using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Domain.Tests.Organizations;

public class OrganizationProfileRulesTests
{
    private static OrganizationProfile With(CompanyDetails? general = null, PeppolDetails? peppol = null)
        => new() { OrganizationId = "org-a", General = general ?? new(), Peppol = peppol ?? new() };

    /// <summary>The fields with an error, after the same normalising the service does.</summary>
    private static IReadOnlyList<string> Errors(OrganizationProfile profile, string? country = "NL")
        => OrganizationProfileRules.Check(OrganizationProfileRules.Normalize(profile), country).Select(e => e.Field).ToList();

    [Fact]
    public void An_empty_profile_is_valid()
        => Assert.Empty(Errors(With()));

    [Fact]
    public void Normalising_trims_compacts_and_empties()
    {
        var profile = OrganizationProfileRules.Normalize(With(
            new CompanyDetails
            {
                LegalName = "  Groen B.V. ",
                VatIdentifier = "nl 1234.56.789.b01",
                LegalRegistrationId = "1234 5678",
                LegalRegistrationScheme = "0106",
                Iban = "nl91 abna 0417 1643 00",
                Bic = " abnanl2a ",
                StreetName = "   ",
            },
            new PeppolDetails { EndpointId = " 12345678 ", EndpointScheme = "0106" }));

        Assert.Equal("Groen B.V.", profile.General.LegalName);
        Assert.Equal("NL123456789B01", profile.General.VatIdentifier);
        Assert.Equal("12345678", profile.General.LegalRegistrationId);
        Assert.Equal("NL91ABNA0417164300", profile.General.Iban);
        Assert.Equal("ABNANL2A", profile.General.Bic);
        Assert.Null(profile.General.StreetName);
        Assert.Equal("0106", profile.General.LegalRegistrationScheme);
        Assert.Equal("12345678", profile.Peppol.EndpointId);
        Assert.Equal("0106", profile.Peppol.EndpointScheme);
    }

    [Fact]
    public void A_scheme_without_a_value_is_dropped()
    {
        var profile = OrganizationProfileRules.Normalize(With(
            new CompanyDetails { LegalRegistrationScheme = "0106" },
            new PeppolDetails { EndpointScheme = "0106" }));

        Assert.Null(profile.General.LegalRegistrationScheme);
        Assert.Null(profile.Peppol.EndpointScheme);
    }

    [Theory]
    [InlineData("NL123456789B01", "NL", true)]
    [InlineData("NL12345678B01", "NL", false)]
    [InlineData("BE0123456749", "BE", true)]
    [InlineData("BE0123456748", "BE", false)]
    [InlineData("BE0123456749", "NL", false)] // a Dutch organisation with a Belgian number
    [InlineData("M90000000X", "SG", true)]    // no prefix rule outside NL, BE and GB
    public void The_vat_number_has_its_countrys_shape(string vat, string country, bool valid)
        => Assert.Equal(valid, !Errors(With(new CompanyDetails { VatIdentifier = vat }), country).Contains("general.vatIdentifier"));

    [Theory]
    [InlineData("12345678", "0106", null)]
    [InlineData("1234567", "0106", "general.legalRegistrationId")]
    [InlineData("0123456749", "0208", null)]
    [InlineData("0123456748", "0208", "general.legalRegistrationId")]
    [InlineData("12345678", null, "general.legalRegistrationScheme")]
    [InlineData("12345678", "12", "general.legalRegistrationScheme")]
    [InlineData("abc", "0106", "general.legalRegistrationId")]
    public void The_registration_number_fits_its_scheme(string id, string? scheme, string? error)
    {
        var errors = Errors(With(new CompanyDetails { LegalRegistrationId = id, LegalRegistrationScheme = scheme }));

        if (error is null)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.Equal(new[] { error }, errors);
        }
    }

    [Theory]
    [InlineData("NL91ABNA0417164300", true)]
    [InlineData("BE68 5390 0754 7034", true)]
    [InlineData("NL91ABNA0417164301", false)]
    [InlineData("NOTANIBAN", false)]
    public void An_iban_passes_the_mod_97_check(string iban, bool valid)
        => Assert.Equal(valid, !Errors(With(new CompanyDetails { Iban = iban })).Contains("general.iban"));

    [Theory]
    [InlineData("30", true)]
    [InlineData("58", true)]
    [InlineData("98", false)]
    public void The_payment_means_is_a_uncl_4461_code(string code, bool valid)
        => Assert.Equal(valid, !Errors(With(new CompanyDetails { PaymentMeansCode = code })).Contains("general.paymentMeansCode"));

    [Theory]
    [InlineData("0106", true)]
    [InlineData(null, false)]
    [InlineData("0217", false)]
    public void A_peppol_id_needs_a_valid_scheme(string? scheme, bool valid)
        => Assert.Equal(valid, !Errors(With(peppol: new PeppolDetails { EndpointId = "12345678", EndpointScheme = scheme })).Contains("peppol.endpointScheme"));

    [Fact]
    public void Iban_is_checked_on_its_own()
    {
        Assert.True(Iban.IsValid("NL91ABNA0417164300"));
        Assert.False(Iban.IsValid("nl91abna0417164300")); // the check takes the compacted, upper-case form
        Assert.False(Iban.IsValid(null));
    }
}
