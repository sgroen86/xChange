using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class InvoiceEnrichmentTests
{
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeProfileStore _profiles = new();
    private readonly FakePlatformStore _platform = new();
    private readonly InvoiceEnrichment _enrichment;

    public InvoiceEnrichmentTests()
    {
        _enrichment = new InvoiceEnrichment(
            TestProfiles.Over(_settings, _profiles, _platform),
            new RelationService(new FakeRelationStore(), new TestClock(DateTimeOffset.UnixEpoch), new InvoiceRulesetRegistry()));
        _platform.SaveOrganizationAsync(new Organization
        {
            OrganizationId = "org-a",
            Name = "Groen",
            CountryCode = "NL",
            CreatedAt = DateTimeOffset.UnixEpoch,
        }).GetAwaiter().GetResult();
    }

    /// <summary>The PEPPOL id where it lived before the profile.</summary>
    private Task Configure(string endpoint, string scheme)
        => _settings.SaveAsync(new OrganizationSettings { OrganizationId = "org-a", EndpointId = endpoint, EndpointScheme = scheme });

    private Task Profile(CompanyDetails general, PeppolDetails? peppol = null)
        => _profiles.SaveAsync(new OrganizationProfile { OrganizationId = "org-a", General = general, Peppol = peppol ?? new() });

    /// <summary>As when a PDF is read.</summary>
    private Task<EnrichmentResult> Read(CanonicalInvoiceDraft draft, string organizationId = "org-a")
        => _enrichment.EnrichAsync(organizationId, draft, SellerFill.ProfileWins);

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
    public async Task Checking_or_exporting_never_overwrites_a_value_from_the_document()
    {
        await Configure("12345678", "0106");
        await Profile(new CompanyDetails { VatIdentifier = "NL123456789B01" }, new PeppolDetails { EndpointId = "12345678", EndpointScheme = "0106" });

        var result = await _enrichment.EnrichAsync("org-a", new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty
            {
                VatIdentifier = "NL999999999B01",
                ElectronicAddress = "NL857231409B01",
                ElectronicAddressScheme = "9944",
            },
        });

        Assert.Equal("NL999999999B01", result.Draft.Seller!.VatIdentifier);
        Assert.Equal("NL857231409B01", result.Draft.Seller.ElectronicAddress);
        Assert.DoesNotContain(result.Notices, n => n.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task Another_organisations_settings_are_not_used()
    {
        await Configure("12345678", "0106");

        var result = await _enrichment.EnrichAsync("org-b", new CanonicalInvoiceDraft());

        Assert.Null(result.Draft.Seller?.ElectronicAddress);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Reading_a_pdf_the_profile_replaces_what_the_model_read_and_says_so()
    {
        await Profile(new CompanyDetails { VatIdentifier = "NL123456789B01", Iban = "NL91ABNA0417164300" });

        var result = await Read(new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { VatIdentifier = "NL123456789B02" },
            Payment = new CanonicalPaymentDetails { Iban = "NL91ABNA0417164301" },
        });

        Assert.Equal("NL123456789B01", result.Draft.Seller!.VatIdentifier);
        Assert.Equal("NL91ABNA0417164300", result.Draft.Payment!.Iban);
        var warnings = result.Notices.Where(n => n.Severity == ValidationSeverity.Warning).ToList();
        Assert.Equal(new[] { "payment.iban", "seller.vatIdentifier" }, warnings.Select(n => n.Path).Order());
        Assert.Contains("NL123456789B02", warnings.Single(n => n.Path == "seller.vatIdentifier").Message);
        Assert.All(warnings, n => Assert.Equal("ENRICH-PROFILE", n.Code));
    }

    [Fact]
    public async Task Reading_a_pdf_an_empty_field_is_filled_with_an_info_notice()
    {
        await Profile(new CompanyDetails { LegalName = "Groen B.V.", StreetName = "Hoofdstraat 1" });

        var result = await Read(new CanonicalInvoiceDraft());

        Assert.Equal("Groen B.V.", result.Draft.Seller!.Name);
        Assert.Equal("Hoofdstraat 1", result.Draft.Seller.Address!.StreetName);
        Assert.Equal("NL", result.Draft.Seller.Address.CountryCode);
        Assert.Null(result.Draft.Payment);
        Assert.All(result.Notices, n => Assert.Equal(ValidationSeverity.Info, n.Severity));
        Assert.Contains(result.Notices, n => n.Path == "seller.address.countryCode");
    }

    [Fact]
    public async Task The_same_value_written_differently_is_no_conflict()
    {
        await Profile(new CompanyDetails { VatIdentifier = "NL123456789B01" });

        var result = await Read(new CanonicalInvoiceDraft { Seller = new CanonicalParty { VatIdentifier = "nl 1234.56.789.b01" } });

        Assert.Equal("NL123456789B01", result.Draft.Seller!.VatIdentifier);
        Assert.DoesNotContain(result.Notices, n => n.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task Contact_and_payment_means_are_only_filled_when_empty()
    {
        await Profile(new CompanyDetails { ContactName = "Jan Jansen", PaymentMeansCode = "58" });

        var result = await Read(new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { ContactName = "Piet Pieters" },
            Payment = new CanonicalPaymentDetails { PaymentMeansCode = "30" },
        });

        Assert.Equal("Piet Pieters", result.Draft.Seller!.ContactName);
        Assert.Equal("30", result.Draft.Payment!.PaymentMeansCode);
        Assert.DoesNotContain(result.Notices, n => n.Path is "seller.contactName" or "payment.paymentMeansCode");
    }

    [Fact]
    public async Task Without_a_saved_profile_the_seller_is_left_as_read()
    {
        var seller = new CanonicalParty { Name = "Wij" };

        var result = await Read(new CanonicalInvoiceDraft { Seller = seller });

        Assert.Same(seller, result.Draft.Seller);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Another_organisations_profile_is_not_used()
    {
        await Profile(new CompanyDetails { LegalName = "Groen B.V.", VatIdentifier = "NL123456789B01" });

        var result = await Read(new CanonicalInvoiceDraft(), organizationId: "org-b");

        Assert.Null(result.Draft.Seller);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Without_a_saved_profile_the_old_peppol_id_never_overwrites_the_document()
    {
        await Configure("12345678", "0106");

        var result = await Read(new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { ElectronicAddress = "NL857231409B01", ElectronicAddressScheme = "9944" },
        });

        Assert.Equal(("NL857231409B01", "9944"), (result.Draft.Seller!.ElectronicAddress, result.Draft.Seller.ElectronicAddressScheme));
        Assert.DoesNotContain(result.Notices, n => n.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task A_postcode_with_or_without_a_space_is_the_same_value()
    {
        await Profile(new CompanyDetails { PostalZone = "1234AB" });

        var result = await Read(new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { Address = new CanonicalAddress { PostalZone = "1234 AB" } },
        });

        Assert.Equal("1234AB", result.Draft.Seller!.Address!.PostalZone);
        Assert.DoesNotContain(result.Notices, n => n.Severity == ValidationSeverity.Warning);
    }
}
