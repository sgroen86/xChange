using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Networks;

namespace InvoicePlatform.Domain.Tests.Networks;

public class PeppolIdentifiersTests
{
    private static string[] Candidates(CanonicalParty party) => [.. PeppolIdentifiers.CandidatesFor(party).Select(c => c.ToString())];

    [Fact]
    public void The_given_electronic_address_comes_first_then_vat_then_registration()
    {
        var party = new CanonicalParty
        {
            ElectronicAddress = "87654321",
            ElectronicAddressScheme = "0106",
            VatIdentifier = "nl 8149.12.345 b01",
            LegalRegistrationId = "8765 4321",
            LegalRegistrationScheme = "0106",
            Address = new CanonicalAddress { CountryCode = "NL" },
        };

        // The registration number is the same address as the electronic one, so it is asked once.
        Assert.Equal(["0106:87654321", "9944:NL814912345B01"], Candidates(party));
    }

    [Fact]
    public void A_belgian_vat_number_is_also_tried_as_enterprise_number()
        => Assert.Equal(["9925:BE0477472701", "0208:0477472701"],
            Candidates(new CanonicalParty { VatIdentifier = "BE 0477.472.701" }));

    [Fact]
    public void A_uk_vat_number_uses_9932()
        => Assert.Equal(["9932:GB123456789"], Candidates(new CanonicalParty { VatIdentifier = "GB123456789" }));

    [Theory]
    [InlineData("NL", "12345678", "0106:12345678")]
    [InlineData("BE", "0477.472.701", "0208:0477472701")]
    [InlineData("SG", "201912345K", "0195:SGUEN201912345K")]
    public void Without_a_scheme_the_registration_is_read_by_country(string country, string id, string expected)
        => Assert.Equal([expected], Candidates(new CanonicalParty { LegalRegistrationId = id, Address = new CanonicalAddress { CountryCode = country } }));

    [Fact]
    public void A_name_alone_gives_nothing_to_look_up()
        => Assert.Empty(Candidates(new CanonicalParty { Name = "Van Dijk Techniek B.V." }));

    [Fact]
    public void An_electronic_address_without_a_valid_scheme_is_not_tried()
        => Assert.Empty(Candidates(new CanonicalParty { ElectronicAddress = "87654321", ElectronicAddressScheme = "KVK" }));
}

public class PeppolDocumentTypesTests
{
    private static readonly InvoiceRulesetRegistry Registry = new();

    private static DocumentTypeId Invoice(string customization, string scheme = "busdox-docid-qns")
        => new(scheme, $"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##{customization}::2.1");

    [Fact]
    public void Exact_invoice_registrations_map_to_formats()
    {
        var accepted = PeppolDocumentTypes.AcceptedRulesets(
            [
                Invoice("urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"),
                Invoice("urn:cen.eu:en16931:2017#compliant#urn:fdc:nen.nl:nlcius:v1.0"),
                new DocumentTypeId("busdox-docid-qns", "urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2::ApplicationResponse##urn:fdc:peppol.eu:poacc:trns:invoice_response:3::2.1"),
            ],
            Registry.All);

        Assert.Equal(["peppol-bis-billing-3.0", "nlcius-si-ubl-2.0"], accepted);
    }

    [Fact]
    public void A_pint_wildcard_registration_accepts_pint_sg()
        => Assert.Equal(["pint-sg-billing-1"],
            PeppolDocumentTypes.AcceptedRulesets([Invoice("urn:peppol:pint:billing-1*", "peppol-doctype-wildcard")], Registry.All));

    [Fact]
    public void A_wildcard_only_counts_under_the_wildcard_scheme()
        => Assert.Empty(PeppolDocumentTypes.AcceptedRulesets([Invoice("urn:peppol:pint:billing-1*")], Registry.All));

    [Fact]
    public void Credit_notes_alone_do_not_make_a_format_accepted()
        => Assert.Empty(PeppolDocumentTypes.AcceptedRulesets(
            [new DocumentTypeId("busdox-docid-qns", "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2::CreditNote##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1")],
            Registry.All));
}
