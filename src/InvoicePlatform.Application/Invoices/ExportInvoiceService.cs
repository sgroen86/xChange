using System.Text;
using System.Text.RegularExpressions;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Ubl;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Checking a draft against a ruleset, and exporting a booked invoice as an
/// e-invoice. The file is made from the invoice as it is stored, so what is
/// downloaded always matches the record.
/// </summary>
public sealed partial class ExportInvoiceService(
    IInvoiceRepository invoices,
    IOrganizationSettingsStore settings,
    InvoiceRulesetRegistry rulesets,
    InvoiceEnrichment enrichment,
    UblInvoiceWriter writer)
{
    public const string MediaType = "application/xml";

    /// <summary>Checks a draft as it is being edited. Nothing is stored.</summary>
    public async Task<CheckOutcome> CheckAsync(
        string organizationId,
        string rulesetId,
        CanonicalInvoiceDraft draft,
        CancellationToken cancellationToken = default)
    {
        var ruleset = rulesets.Find(rulesetId);
        if (ruleset is null)
        {
            return new CheckOutcome(RulesetFound: false, []);
        }

        var enriched = await enrichment.EnrichAsync(organizationId, draft, cancellationToken).ConfigureAwait(false);
        var result = ValidatedCanonicalInvoice.Validate(ruleset, enriched.Draft);
        return new CheckOutcome(RulesetFound: true, result.Violations);
    }

    /// <param name="requestedRulesetId">The user's choice for this download; null means the organisation's default.</param>
    public async Task<ExportOutcome> ExportAsync(
        string organizationId,
        string invoiceId,
        string? requestedRulesetId,
        CancellationToken cancellationToken = default)
    {
        var organization = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var ruleset = rulesets.Resolve(requestedRulesetId, organization?.DefaultRulesetId);
        if (ruleset is null)
        {
            return ExportOutcome.UnknownRuleset;
        }

        // Scoped by organisation: another tenant's invoice is simply not found.
        var stored = await invoices.GetAsync(organizationId, invoiceId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return ExportOutcome.NotFound;
        }

        var enriched = await enrichment.EnrichAsync(organizationId, stored.Invoice, cancellationToken).ConfigureAwait(false);
        var result = ValidatedCanonicalInvoice.Validate(ruleset, enriched.Draft);
        if (result.Invoice is null)
        {
            return new ExportOutcome(ExportOutcomeKind.Incomplete, Violations: result.Violations);
        }

        var xml = writer.Write(result.Invoice);
        return new ExportOutcome(
            ExportOutcomeKind.Exported,
            FileName: FileName(stored.Invoice.InvoiceNumber ?? invoiceId, ruleset.Id),
            Content: new UTF8Encoding(false).GetBytes(xml));
    }

    /// <summary>"F-2026/041" + ruleset -> "F-2026-041-peppol-bis-billing-3.0.xml".</summary>
    private static string FileName(string invoiceNumber, string rulesetId)
        => $"{UnsafeFileNameCharacters().Replace(invoiceNumber.Trim(), "-")}-{rulesetId}.xml";

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeFileNameCharacters();
}

/// <param name="RulesetFound">False when the requested ruleset does not exist.</param>
public sealed record CheckOutcome(bool RulesetFound, IReadOnlyList<RuleViolation> Violations);

public enum ExportOutcomeKind
{
    Exported,
    Incomplete,
    NotFound,
    UnknownRuleset,
}

public sealed record ExportOutcome(
    ExportOutcomeKind Kind,
    string? FileName = null,
    byte[]? Content = null,
    IReadOnlyList<RuleViolation>? Violations = null)
{
    public static readonly ExportOutcome NotFound = new(ExportOutcomeKind.NotFound);

    public static readonly ExportOutcome UnknownRuleset = new(ExportOutcomeKind.UnknownRuleset);
}
