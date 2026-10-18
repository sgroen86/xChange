using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The conversion API end to end: keys made by an admin, accepted by the
/// conversion endpoints and by nothing else, scoped to their organisation.
/// </summary>
public class ConversionEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public ConversionEndpointTests(SignedInFactory factory) => _factory = factory;

    private async Task<string> CreateKey(string token = "admin-a", string name = "ERP")
    {
        var response = await _factory.As(token).PostAsJsonAsync("/api/v1/organization/api-keys", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
    }

    private static ByteArrayContent Pdf(string content = "%PDF-1.4 test")
    {
        var body = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes(content));
        body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return body;
    }

    private async Task<string> Convert(string key, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/conversions{query}") { Content = Pdf() };
        request.Headers.Add("X-File-Name", "factuur-41.pdf");
        var response = await _factory.As(key).SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conversionId").GetString()!;
        Assert.Equal($"/api/v1/conversions/{id}", response.Headers.Location?.OriginalString);
        return id;
    }

    [Fact]
    public async Task A_key_is_shown_once_and_listed_by_its_prefix_only()
    {
        var key = await CreateKey(name: "Exact koppeling");

        var list = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/api-keys");
        var item = list.EnumerateArray().Single(e => key.StartsWith(e.GetProperty("prefix").GetString()!, StringComparison.Ordinal));

        Assert.StartsWith("xck_", key, StringComparison.Ordinal);
        Assert.Equal("Exact koppeling", item.GetProperty("name").GetString());
        Assert.DoesNotContain(key, list.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Only_an_admin_manages_keys(string token)
    {
        var client = _factory.As(token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/organization/api-keys", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/organization/api-keys")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/v1/organization/api-keys/any")).StatusCode);
    }

    [Fact]
    public async Task A_key_sends_a_pdf_and_follows_its_conversion()
    {
        var key = await CreateKey();

        var id = await Convert(key, "?format=peppol-bis-billing-3.0");
        var status = await _factory.As(key).GetFromJsonAsync<JsonElement>($"/api/v1/conversions/{id}");
        var xml = await _factory.As(key).GetAsync($"/api/v1/conversions/{id}/xml");

        Assert.Contains((SignedInFactory.OrgA, id), _factory.Imports.Queued);
        var record = (await _factory.Imports.GetAsync(SignedInFactory.OrgA, id))!;
        Assert.Equal((ImportSource.Api, "factuur-41.pdf", "peppol-bis-billing-3.0"), (record.Source, record.FileName, record.RulesetId));
        Assert.Equal("queued", status.GetProperty("status").GetString());
        Assert.Equal("peppol-bis-billing-3.0", status.GetProperty("format").GetString());
        Assert.Equal(HttpStatusCode.Conflict, xml.StatusCode);
    }

    [Fact]
    public async Task A_ready_conversion_downloads_its_file()
    {
        var key = await CreateKey();
        var id = await Convert(key);
        await _factory.Invoices.SaveAsync(new StoredInvoice
        {
            InvoiceId = id,
            OrganizationId = SignedInFactory.OrgA,
            Status = InvoiceStatus.Booked,
            Invoice = TestInvoices.Complete(),
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedByUserId = "apikey:k",
        });

        var status = await _factory.As(key).GetFromJsonAsync<JsonElement>($"/api/v1/conversions/{id}");
        var xml = await _factory.As(key).GetAsync($"/api/v1/conversions/{id}/xml");

        Assert.Equal("ready", status.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, xml.StatusCode);
        Assert.Equal("application/xml", xml.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/v1/invoices")]
    [InlineData("/api/v1/relations")]
    [InlineData("/api/v1/organization/settings")]
    [InlineData("/api/v1/imports")]
    [InlineData("/api/v1/organization/api-keys")]
    [InlineData("/api/v1/auth/me")]
    public async Task A_key_opens_nothing_but_the_conversion_endpoints(string route)
    {
        var key = await CreateKey();

        var response = await _factory.As(key).GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_key_cannot_make_another_key_or_book()
    {
        var key = await CreateKey();
        var client = _factory.As(key);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/organization/api-keys", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/imports", new MultipartFormDataContent { { Pdf(), "file", "f.pdf" } })).StatusCode);
    }

    [Fact]
    public async Task A_signed_in_user_is_not_a_key()
    {
        var response = await _factory.As("admin-a").PostAsync("/api/v1/conversions", Pdf());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("xck_made-up")]
    [InlineData("")]
    public async Task An_unknown_or_missing_key_is_refused(string key)
    {
        var client = key.Length == 0 ? _factory.CreateClient() : _factory.As(key);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/conversions", Pdf())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/conversions/x")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/conversions/x/xml")).StatusCode);
    }

    [Fact]
    public async Task Another_organisations_key_does_not_find_the_conversion()
    {
        var keyA = await CreateKey();
        var keyB = await CreateKey("admin-b");
        var id = await Convert(keyA);

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.As(keyB).GetAsync($"/api/v1/conversions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.As(keyB).GetAsync($"/api/v1/conversions/{id}/xml")).StatusCode);
    }

    [Fact]
    public async Task An_ordinary_import_is_not_a_conversion()
    {
        var key = await CreateKey();
        var upload = new MultipartFormDataContent { { Pdf(), "file", "f.pdf" } };
        var submitted = await _factory.As("user-a").PostAsync("/api/v1/imports", upload);
        var importId = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("importId").GetString();

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.As(key).GetAsync($"/api/v1/conversions/{importId}")).StatusCode);
    }

    [Fact]
    public async Task A_trial_converts_but_cannot_download()
    {
        var key = await CreateKey("admin-b");
        await _factory.Usage.SetLimitAsync(SignedInFactory.OrgB, 25, trial: true);
        var id = await Convert(key);

        var response = await _factory.As(key).GetAsync($"/api/v1/conversions/{id}/xml");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task Not_a_pdf_and_unknown_formats_are_refused()
    {
        var key = await CreateKey();
        var client = _factory.As(key);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/conversions", Pdf("hello"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/conversions", Pdf(""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/conversions?format=xrechnung-9", Pdf())).StatusCode);
    }

    [Fact]
    public async Task A_pdf_over_the_limit_is_refused()
    {
        var key = await CreateKey();
        var large = new byte[(4 * 1024 * 1024) + 1];
        "%PDF-1.4"u8.CopyTo(large);

        var response = await _factory.As(key).PostAsync("/api/v1/conversions", new ByteArrayContent(large));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task A_revoked_key_stops_working()
    {
        var key = await CreateKey();
        var list = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/api-keys");
        var keyId = list.EnumerateArray().Single(e => key.StartsWith(e.GetProperty("prefix").GetString()!, StringComparison.Ordinal))
            .GetProperty("keyId").GetString();

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.As("admin-b").DeleteAsync($"/api/v1/organization/api-keys/{keyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.As("admin-a").DeleteAsync($"/api/v1/organization/api-keys/{keyId}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.As(key).PostAsync("/api/v1/conversions", Pdf())).StatusCode);
    }
}
