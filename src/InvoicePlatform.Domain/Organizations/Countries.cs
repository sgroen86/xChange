using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Pint;

namespace InvoicePlatform.Domain.Organizations;

/// <summary>A country an organisation can be established in.</summary>
/// <param name="Code">ISO 3166-1 alpha-2. The United Kingdom is "GB", as PEPPOL expects.</param>
/// <param name="Name">Dutch, for the screens.</param>
/// <param name="DefaultRulesetId">The export format an organisation there starts with.</param>
public sealed record Country(string Code, string Name, string DefaultRulesetId);

/// <summary>
/// The countries xChange serves. This is the one place a country is added;
/// later pieces of work hang the per-country defaults (export format,
/// identifier schemes) on <see cref="Country"/>, not on new lists elsewhere.
/// </summary>
public static class Countries
{
    public static readonly IReadOnlyList<Country> All =
    [
        new("SG", "Singapore", PintSgBilling1Ruleset.RulesetId),
        new("GB", "Verenigd Koninkrijk", PeppolBisBilling30Ruleset.RulesetId),
        new("NL", "Nederland", PeppolBisBilling30Ruleset.RulesetId),
        new("BE", "België", PeppolBisBilling30Ruleset.RulesetId),
    ];

    public static Country? Find(string? code)
        => All.FirstOrDefault(country => string.Equals(country.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsSupported(string? code) => Find(code) is not null;
}
