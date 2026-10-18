using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Peppol;

/// <summary>
/// PEPPOL BIS Billing 3.0, following rules release 3.0.21 (mandatory from
/// 17 August 2026): the EN 16931 core, PEPPOL's own rules, and the Dutch
/// national rules when the seller is in the Netherlands.
///
/// The point release is not in the document: 3.0.21 rejects a CustomizationID
/// that carries version information beyond "3.0".
/// </summary>
public sealed class PeppolBisBilling30Ruleset : IInvoiceRuleset
{
    public const string RulesetId = "peppol-bis-billing-3.0";

    public string Id => RulesetId;

    public string DisplayName => "PEPPOL BIS Billing 3.0";

    public string RulesRelease => "3.0.21";

    public InvoiceSyntax Syntax => InvoiceSyntax.Ubl21;

    public string CustomizationId => "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0";

    public string ProfileId => "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return En16931CoreRules.Check(invoice)
            .Concat(PeppolRules.Check(invoice))
            .Concat(DutchRules.Check(invoice))
            .DistinctBy(violation => (violation.RuleId, violation.Path))
            .OrderBy(violation => violation.Path, StringComparer.Ordinal)
            .ThenBy(violation => violation.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}
