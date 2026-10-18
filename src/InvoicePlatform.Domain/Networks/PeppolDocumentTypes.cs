using InvoicePlatform.Domain.EInvoicing;

namespace InvoicePlatform.Domain.Networks;

/// <summary>
/// Which of xChange's formats a receiver accepts, from the document types it
/// registered. A format is accepted when the receiver takes its invoices: a
/// document type "...Invoice-2::Invoice##&lt;customization&gt;::2.1" whose
/// customization is the ruleset's CustomizationID - or, for a wildcard
/// registration ("peppol-doctype-wildcard", "...##urn:peppol:pint:billing-1*::2.1"),
/// starts with the part before the asterisk.
/// </summary>
public static class PeppolDocumentTypes
{
    private const string InvoiceRoot = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##";
    private const string Wildcard = "peppol-doctype-wildcard";

    public static IReadOnlyList<string> AcceptedRulesets(IEnumerable<DocumentTypeId> documentTypes, IEnumerable<IInvoiceRuleset> rulesets)
    {
        var invoiceTypes = documentTypes
            .Select(type => (type.Scheme, Customization: CustomizationOf(type.Value)))
            .Where(type => type.Customization is not null)
            .ToList();

        return rulesets
            .Where(ruleset => invoiceTypes.Any(type => Accepts(type.Scheme, type.Customization!, ruleset.CustomizationId)))
            .Select(ruleset => ruleset.Id)
            .ToList();
    }

    private static bool Accepts(string scheme, string customization, string customizationId)
    {
        if (string.Equals(customization, customizationId, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(scheme, Wildcard, StringComparison.OrdinalIgnoreCase)
            && customization.EndsWith('*')
            && customizationId.StartsWith(customization[..^1], StringComparison.Ordinal);
    }

    /// <summary>"...Invoice-2::Invoice##urn:x::2.1" -> "urn:x"; null for anything but a UBL invoice.</summary>
    private static string? CustomizationOf(string value)
    {
        if (!value.StartsWith(InvoiceRoot, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = value[InvoiceRoot.Length..];
        var version = rest.LastIndexOf("::", StringComparison.Ordinal);
        return version <= 0 ? null : rest[..version];
    }
}
