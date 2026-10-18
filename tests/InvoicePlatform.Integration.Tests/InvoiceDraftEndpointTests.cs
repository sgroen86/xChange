using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

/// <summary>Saving a concept and deleting an invoice, through the API.</summary>
public class InvoiceDraftEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public InvoiceDraftEndpointTests(SignedInFactory factory) => _factory = factory;

    private static object Request(string? invoiceId = null) => new
    {
        invoiceId,
        canonicalInvoice = new { invoiceNumber = "C-1", currencyCode = "EUR", lines = new[] { new { description = "Werk", netAmount = 10.00m } } },
        documentBase64 = Convert.ToBase64String([1, 2, 3]),
        sourceFileName = "concept.pdf",
        providerModel = "m",
    };

    [Fact]
    public async Task A_saved_concept_is_listed_as_a_draft_and_is_not_exported()
    {
        var client = _factory.As("user-a");

        var response = await client.PostAsJsonAsync("/api/v1/invoices/drafts", Request());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("invoiceId").GetString();
        var export = await client.GetAsync($"/api/v1/invoices/{id}/export?ruleset=peppol-bis-billing-3.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, export.StatusCode);
    }

    [Fact]
    public async Task A_booked_invoice_is_not_saved_as_a_concept()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("user-a").PostAsJsonAsync("/api/v1/invoices/drafts", Request(id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_invoice_removes_it()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());
        var client = _factory.As("user-a");

        var delete = await client.DeleteAsync($"/api/v1/invoices/{id}");
        var after = await client.GetAsync($"/api/v1/invoices/{id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Another_organisation_cannot_delete_an_invoice()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var delete = await _factory.As("admin-b").DeleteAsync($"/api/v1/invoices/{id}");

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.As("user-a").GetAsync($"/api/v1/invoices/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_read_only_account_cannot_save_or_delete()
    {
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());
        var client = _factory.As("readonly-a");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/invoices/drafts", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/v1/invoices/{id}")).StatusCode);
    }

    [Fact]
    public async Task Saving_and_deleting_require_a_login()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/invoices/drafts", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/invoices/some-id")).StatusCode);
    }
}
