using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Pint;
using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.Organizations;

/// <summary>A country an organisation can be established in.</summary>
/// <param name="Code">ISO 3166-1 alpha-2. The United Kingdom is "GB", as PEPPOL expects.</param>
/// <param name="DefaultRulesetId">The export format an organisation there starts with.</param>
/// <param name="TaxNumberIsVatIdentifier">
/// True where a company's tax number is a VAT number in the EN 16931 sense
/// (BT-31). False for Singapore: a GST registration number is a tax
/// registration identifier (BT-32) once the invoice leaves PINT-SG.
/// </param>
public sealed record Country(string Code, string DefaultRulesetId, bool TaxNumberIsVatIdentifier = true)
{
    /// <summary>For the screens, in the language of the request.</summary>
    public string Name
    {
        get
        {
            var text = CountryText.Current;
            return Code switch
            {
                "SG" => text.Singapore,
                "GB" => text.UnitedKingdom,
                "NL" => text.Netherlands,
                "BE" => text.Belgium,
                _ => Code,
            };
        }
    }
}

/// <summary>
/// The countries xChange serves. This is the one place a country is added;
/// later pieces of work hang the per-country defaults (export format,
/// identifier schemes) on <see cref="Country"/>, not on new lists elsewhere.
/// </summary>
public static class Countries
{
    public static readonly IReadOnlyList<Country> All =
    [
        new("SG", PintSgBilling1Ruleset.RulesetId, TaxNumberIsVatIdentifier: false),
        new("GB", PeppolBisBilling30Ruleset.RulesetId),
        new("NL", PeppolBisBilling30Ruleset.RulesetId),
        new("BE", PeppolBisBilling30Ruleset.RulesetId),
    ];

    public static Country? Find(string? code)
        => All.FirstOrDefault(country => string.Equals(country.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsSupported(string? code) => Find(code) is not null;
}

/// <summary>The country names, one member per country in <see cref="Countries.All"/>.</summary>
internal interface ICountryText
{
    string Singapore { get; }

    string UnitedKingdom { get; }

    string Netherlands { get; }

    string Belgium { get; }
}

internal sealed class CountryTextNl : ICountryText
{
    public string Singapore => "Singapore";

    public string UnitedKingdom => "Verenigd Koninkrijk";

    public string Netherlands => "Nederland";

    public string Belgium => "België";
}

internal sealed class CountryTextEn : ICountryText
{
    public string Singapore => "Singapore";

    public string UnitedKingdom => "United Kingdom";

    public string Netherlands => "Netherlands";

    public string Belgium => "Belgium";
}

internal static class CountryText
{
    private static readonly ICountryText Nl = new CountryTextNl(), En = new CountryTextEn();

    public static ICountryText Current => Texts.Pick(Nl, En);
}
