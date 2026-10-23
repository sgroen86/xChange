using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Integration.Tests;

public class ExportEndpointTests : IClassFixture<SignedInFactory>
{
    private const string Peppol = "peppol-bis-billing-3.0";

    private readonly SignedInFactory _factory;

    public ExportEndpointTests(SignedInFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/v1/invoices/some-id/export")]
    [InlineData("POST", "/api/v1/rulesets/peppol-bis-billing-3.0/check")]
    [InlineData("GET", "/api/v1/organization/settings")]
    [InlineData("PUT", "/api/v1/organization/settings")]
    public async Task Every_route_requires_a_login(string method, string path)
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : new StringContent("{}", Encoding.UTF8, "application/json"),
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_complete_invoice_downloads_as_a_named_xml_file()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("user-a").GetAsync($"/api/v1/invoices/{id}/export?ruleset={Peppol}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("F-2026-041-peppol-bis-billing-3.0.xml", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.StartsWith("<?xml", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Downloading_needs_a_running_plan_or_credits_and_stops_when_the_plan_ends()
    {
        using var factory = new SignedInFactory();
        await factory.Usage.SetLimitAsync(SignedInFactory.OrgB, 1, trial: true);
        var id = await factory.SeedInvoiceAsync(SignedInFactory.OrgB, TestInvoices.Complete());
        var client = factory.As("admin-b");

        async Task<HttpResponseMessage> Export(string language = "nl")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{id}/export");
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
            return await client.SendAsync(request);
        }

        async Task<bool> CanExport()
            => (await client.GetFromJsonAsync<JsonElement>("/api/v1/organization/usage")).GetProperty("canExport").GetBoolean();

        // The trial: refused, in the user's language.
        var refused = await Export("en");
        Assert.Equal(HttpStatusCode.PaymentRequired, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("running plan or purchased credits", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Contains("abonnement of gekocht tegoed", (await (await Export("nl")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(await CanExport());

        // A running plan.
        var org = factory.Usage.For(SignedInFactory.OrgB);
        org.Status = "active";
        org.SubscribedOnce = true;
        org.PaidOnce = true;
        Assert.Equal(HttpStatusCode.OK, (await Export()).StatusCode);
        Assert.True(await CanExport());

        // The plan ended; having paid once is not enough.
        org.Status = "canceled";
        Assert.Equal(HttpStatusCode.PaymentRequired, (await Export()).StatusCode);
        Assert.False(await CanExport());

        // Credits left.
        org.Credits = 2;
        Assert.Equal(HttpStatusCode.OK, (await Export()).StatusCode);
        Assert.True(await CanExport());
    }

    [Fact]
    public async Task A_read_only_account_may_export()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("readonly-a").GetAsync($"/api/v1/invoices/{id}/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_incomplete_invoice_is_refused_with_its_violations()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete() with { BuyerReference = null });

        var response = await _factory.As("admin-a").GetAsync($"/api/v1/invoices/{id}/export");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body.GetProperty("violations").EnumerateArray(),
            v => v.GetProperty("ruleId").GetString() == "PEPPOL-EN16931-R003" && v.GetProperty("path").GetString() == "buyerReference");
    }

    [Fact]
    public async Task Another_organisations_invoice_is_not_found()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("admin-b").GetAsync($"/api/v1/invoices/{id}/export");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_ruleset_is_a_bad_request()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("admin-a").GetAsync($"/api/v1/invoices/{id}/export?ruleset=xrechnung-9");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checking_a_draft_lists_its_violations()
    {
        var response = await _factory.As("readonly-a").PostAsync(
            $"/api/v1/rulesets/{Peppol}/check",
            new StringContent("{\"invoiceNumber\":null,\"lines\":[{\"quantity\":\"2\",\"unitPrice\":\"10.50\"}]}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ruleIds = body.GetProperty("violations").EnumerateArray().Select(v => v.GetProperty("ruleId").GetString()).ToList();
        Assert.Contains("BR-02", ruleIds);
        Assert.Contains("BR-CO-04", ruleIds);
    }

    [Fact]
    public async Task Checking_against_an_unknown_ruleset_is_not_found()
    {
        var response = await _factory.As("admin-a").PostAsync(
            "/api/v1/rulesets/xrechnung-9/check", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_draft_is_a_bad_request()
    {
        var response = await _factory.As("admin-a").PostAsync(
            $"/api/v1/rulesets/{Peppol}/check", new StringContent("{\"lines\":[{\"quantity\":\"twee\"}]}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

internal static class TestInvoices
{
    public static CanonicalInvoiceDraft Complete() => new()
    {
        TypeCode = "380",
        InvoiceNumber = "F-2026-041",
        IssueDate = "2026-10-01",
        CurrencyCode = "EUR",
        BuyerReference = "PO-2026-17",
        Seller = new CanonicalParty
        {
            Name = "Green IT Solutions B.V.",
            VatIdentifier = "NL857231409B01",
            ElectronicAddress = "12345678",
            ElectronicAddressScheme = "0106",
            Address = new CanonicalAddress { StreetName = "Keizersgracht 62", PostalZone = "1015 CS", CityName = "Amsterdam", CountryCode = "NL" },
        },
        Buyer = new CanonicalParty
        {
            Name = "Van Dijk Techniek B.V.",
            ElectronicAddress = "87654321",
            ElectronicAddressScheme = "0106",
            Address = new CanonicalAddress { StreetName = "Industrieweg 24", PostalZone = "3542 AD", CityName = "Utrecht", CountryCode = "NL" },
        },
        Payment = new CanonicalPaymentDetails { PaymentMeansCode = "30", Iban = "NL91ABNA0417164300" },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                Description = "Onderhoudscontract",
                Quantity = 1m,
                UnitCode = "C62",
                UnitPrice = 1250.00m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
        ],
    };
}
