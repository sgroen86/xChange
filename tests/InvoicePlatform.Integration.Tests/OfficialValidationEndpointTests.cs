using System.Net;
using System.Text;
using System.Text.Json;
using InvoicePlatform.Application.Invoices;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Every download goes through the official validator. This class has its own
/// factory because it changes the validator's verdict.
/// </summary>
public class OfficialValidationEndpointTests : IDisposable
{
    private readonly SignedInFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task No_official_verdict_means_no_file()
    {
        _factory.Official.Verdict = OfficialValidation.Unavailable("down");
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("admin-a").GetAsync($"/api/v1/invoices/{id}/export");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("<?xml", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fatal_official_failure_is_a_422_with_the_official_rule()
    {
        _factory.Official.Verdict = new OfficialValidation(true, "3.0.21",
            [new OfficialFailure("fatal", "BR-CL-23", "Unit code MUST be coded according to the UN/ECE Recommendation 20", "/Invoice[1]/InvoiceLine[1]/InvoicedQuantity[1]")]);
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var response = await _factory.As("admin-a").GetAsync($"/api/v1/invoices/{id}/export");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BR-CL-23", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Official_warnings_travel_in_a_header_the_browser_may_read()
    {
        _factory.Official.Verdict = new OfficialValidation(true, "3.0.21",
            [new OfficialFailure("warning", "PEPPOL-COMMON-R054", "KVK format", "/Invoice[1]")]);
        var id = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, TestInvoices.Complete());

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{id}/export");
        request.Headers.Add("Authorization", "Bearer admin-a");
        request.Headers.Add("Origin", "https://greenitsolutions.net");
        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = Assert.Single(response.Headers.GetValues("X-Validation-Warnings"));
        var warnings = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(header)));
        Assert.Equal("PEPPOL-COMMON-R054", warnings.RootElement[0].GetProperty("ruleId").GetString());

        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains("Content-Disposition", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-Validation-Warnings", exposed, StringComparison.OrdinalIgnoreCase);
    }
}
