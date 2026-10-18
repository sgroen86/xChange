using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>The XML syntax a ruleset is expressed in.</summary>
public enum InvoiceSyntax
{
    Ubl21,
}

/// <summary>
/// One e-invoicing ruleset: a specialisation of EN 16931 (PEPPOL BIS, NLCIUS,
/// XRechnung, ...) in one syntax. Adding a ruleset is one class and one
/// registration in <see cref="InvoiceRulesetRegistry"/>.
/// </summary>
public interface IInvoiceRuleset
{
    /// <summary>Stable identifier, used in URLs and settings, e.g. "peppol-bis-billing-3.0".</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>The release of the official rules this implementation follows, e.g. "3.0.21".</summary>
    string RulesRelease { get; }

    InvoiceSyntax Syntax { get; }

    /// <summary>BT-24, written as cbc:CustomizationID.</summary>
    string CustomizationId { get; }

    /// <summary>BT-23, written as cbc:ProfileID.</summary>
    string ProfileId { get; }

    /// <summary>
    /// Checks an invoice whose line nets, VAT breakdown and totals have already
    /// been calculated. Returns every violation found, not only the first.
    /// </summary>
    IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice);
}
