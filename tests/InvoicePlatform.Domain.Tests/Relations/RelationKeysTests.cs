using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Domain.Tests.Relations;

public class RelationKeysTests
{
    private static Relation Known(string id, string? vat = null, string? kvk = null, string? name = null) => new()
    {
        RelationId = id,
        OrganizationId = "org-a",
        Name = name,
        VatIdentifier = vat,
        LegalRegistrationId = kvk,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    [Theory]
    [InlineData("nl 8149.12.345-b01", "NL814912345B01")]
    [InlineData("NL814912345B01", "NL814912345B01")]
    [InlineData(" . ", null)]
    [InlineData(null, null)]
    public void Vat_numbers_are_compared_without_punctuation_or_case(string? input, string? expected)
        => Assert.Equal(expected, RelationKeys.NormalizeVat(input));

    [Theory]
    [InlineData("1234 5678", "12345678")]
    [InlineData("KvK 12345678", "12345678")]
    [InlineData("geen", null)]
    public void Registration_numbers_are_compared_on_their_digits(string input, string? expected)
        => Assert.Equal(expected, RelationKeys.NormalizeRegistration(input));

    [Fact]
    public void Vat_wins_over_a_different_registration_match()
    {
        var relations = new[] { Known("by-kvk", kvk: "12345678"), Known("by-vat", vat: "NL814912345B01") };

        var match = RelationKeys.FindMatch(relations, new CanonicalParty { VatIdentifier = "nl814912345b01", LegalRegistrationId = "12345678" });

        Assert.Equal("by-vat", match?.RelationId);
    }

    [Fact]
    public void Registration_is_used_when_there_is_no_vat_match()
    {
        var relations = new[] { Known("by-kvk", kvk: "12345678") };

        var match = RelationKeys.FindMatch(relations, new CanonicalParty { VatIdentifier = "NL000000000B00", LegalRegistrationId = "1234 5678" });

        Assert.Equal("by-kvk", match?.RelationId);
    }

    [Fact]
    public void The_same_name_without_identifiers_is_not_a_match()
    {
        var relations = new[] { Known("jansen", vat: "NL814912345B01", name: "Jansen B.V.") };

        Assert.Null(RelationKeys.FindMatch(relations, new CanonicalParty { Name = "Jansen B.V." }));
    }

    [Fact]
    public void Nothing_matches_an_absent_party()
        => Assert.Null(RelationKeys.FindMatch([Known("a", vat: "NL1")], null));
}
