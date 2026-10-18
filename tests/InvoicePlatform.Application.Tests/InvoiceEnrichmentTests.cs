using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class InvoiceEnrichmentTests
{
    private readonly FakeSettingsStore _settings = new();
    private readonly InvoiceEnrichment _enrichment;

    public InvoiceEnrichmentTests()
        => _enrichment = new InvoiceEnrichment(_settings);

    private Task Configure(string endpoint, string scheme)
        => _settings.SaveAsync(new OrganizationSettings { OrganizationId = "org-a", EndpointId = endpoint, EndpointScheme = scheme });

    [Fact]
    public async Task The_organisations_endpoint_fills_an_empty_seller_endpoint()
    {
        await Configure("12345678", "0106");

        var result = await _enrichment.EnrichAsync("org-a", new CanonicalInvoiceDraft { Seller = new CanonicalParty { Name = "Wij" } });

        Assert.Equal(("12345678", "0106"), (result.Draft.Seller!.ElectronicAddress, result.Draft.Seller.ElectronicAddressScheme));
        Assert.Equal("Wij", result.Draft.Seller.Name);
        var notice = Assert.Single(result.Notices);
        Assert.Equal(ValidationSeverity.Info, notice.Severity);
        Assert.Equal("seller.electronicAddress", notice.Path);
    }

    [Fact]
    public async Task A_value_from_the_document_is_never_overwritten()
    {
        await Configure("12345678", "0106");

        var result = await _enrichment.EnrichAsync("org-a", new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { ElectronicAddress = "NL857231409B01", ElectronicAddressScheme = "9944" },
        });

        Assert.Equal("NL857231409B01", result.Draft.Seller!.ElectronicAddress);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Another_organisations_settings_are_not_used()
    {
        await Configure("12345678", "0106");

        var result = await _enrichment.EnrichAsync("org-b", new CanonicalInvoiceDraft());

        Assert.Null(result.Draft.Seller?.ElectronicAddress);
        Assert.Empty(result.Notices);
    }
}
