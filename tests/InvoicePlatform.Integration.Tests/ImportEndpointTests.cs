using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoicePlatform.Domain.Imports;

namespace InvoicePlatform.Integration.Tests;

public class ImportEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public ImportEndpointTests(SignedInFactory factory) => _factory = factory;

    private static MultipartFormDataContent Pdf(string name = "factuur.pdf", string content = "%PDF-1.4 test")
    {
        var file = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", name } };
    }

    private async Task<string> Submit(string token = "user-a", string name = "factuur.pdf")
    {
        var response = await _factory.As(token).PostAsync("/api/v1/imports", Pdf(name));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("importId").GetString()!;
    }

    [Fact]
    public async Task A_pdf_is_accepted_queued_and_listed()
    {
        var id = await Submit(name: "batch-1.pdf");

        Assert.Contains((SignedInFactory.OrgA, id), _factory.Imports.Queued);
        var list = await _factory.As("readonly-a").GetFromJsonAsync<JsonElement>("/api/v1/imports");
        var item = list.EnumerateArray().Single(e => e.GetProperty("importId").GetString() == id);
        Assert.Equal("queued", item.GetProperty("status").GetString());
        Assert.Equal("batch-1.pdf", item.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Something_that_is_not_a_pdf_is_refused()
    {
        var response = await _factory.As("user-a").PostAsync("/api/v1/imports", Pdf(content: "hello"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_read_only_account_cannot_submit()
    {
        var response = await _factory.As("readonly-a").PostAsync("/api/v1/imports", Pdf());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Another_organisations_import_is_not_found_anywhere()
    {
        var id = await Submit();
        var other = _factory.As("admin-b");

        Assert.DoesNotContain((await other.GetFromJsonAsync<JsonElement>("/api/v1/imports")).EnumerateArray(),
            e => e.GetProperty("importId").GetString() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/imports/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/imports/{id}/document")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/imports/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/imports/{id}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task A_ready_import_comes_with_its_draft_and_pdf_and_can_be_dismissed()
    {
        var id = await Submit();
        var record = (await _factory.Imports.GetAsync(SignedInFactory.OrgA, id))!;
        await _factory.Imports.SaveAsync(record with
        {
            Status = ImportStatus.Ready,
            ExtractionId = "ext-1",
            Draft = new Domain.Canonical.CanonicalInvoiceDraft { InvoiceNumber = "F-77" },
        });
        var client = _factory.As("user-a");

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/imports/{id}");
        var pdf = await client.GetAsync($"/api/v1/imports/{id}/document");
        var dismissed = await client.DeleteAsync($"/api/v1/imports/{id}");

        Assert.Equal("ready", detail.GetProperty("status").GetString());
        Assert.Equal("F-77", detail.GetProperty("canonicalInvoice").GetProperty("invoiceNumber").GetString());
        Assert.Equal("ext-1", detail.GetProperty("extractionId").GetString());
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);
        Assert.Null(await _factory.Imports.GetAsync(SignedInFactory.OrgA, id));
    }
}
