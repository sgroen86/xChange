using InvoicePlatform.Application.Usage;
using System.Text;
using System.Text.RegularExpressions;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Ubl;
using InvoicePlatform.Domain.Invoices;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Checking a draft against a ruleset, and exporting a booked invoice as an
/// e-invoice. The file is made from the invoice as it is stored, so what is
/// downloaded always matches the record, and every file is run through the
/// complete official rules before it is handed out.
/// </summary>
public sealed partial class ExportInvoiceService(
    IInvoiceRepository invoices,
    IOrganizationSettingsStore settings,
    InvoiceRulesetRegistry rulesets,
    InvoiceEnrichment enrichment,
    UblInvoiceWriter writer,
    IOfficialValidator officialValidator,
    IPlatformStore platform,
    IUsageMeter usage)
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

    /// <param name="requestedRulesetId">
    /// The user's choice for this download; null means the customer's default,
    /// else the organisation's, else its country's.
    /// </param>
    public async Task<ExportOutcome> ExportAsync(
        string organizationId,
        string invoiceId,
        string? requestedRulesetId,
        CancellationToken cancellationToken = default)
    {
        if (requestedRulesetId is not null && rulesets.Find(requestedRulesetId) is null)
        {
            return ExportOutcome.UnknownRuleset;
        }

        // A trial checks everything but takes nothing away: the file needs a plan.
        if ((await usage.GetAsync(organizationId, cancellationToken).ConfigureAwait(false)).Trial)
        {
            return ExportOutcome.PlanRequired;
        }

        // Scoped by organisation: another tenant's invoice is simply not found.
        var stored = await invoices.GetAsync(organizationId, invoiceId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return ExportOutcome.NotFound;
        }

        // A concept is work in progress: the file is made from what was booked.
        if (stored.Status != InvoiceStatus.Booked)
        {
            return ExportOutcome.NotBooked;
        }

        var outcome = await ProduceAsync(organizationId, invoiceId, stored.Invoice, requestedRulesetId, cancellationToken).ConfigureAwait(false);
        return outcome.Kind == ExportOutcomeKind.Exported
            ? outcome with { FileName = FileName(stored.Invoice.InvoiceNumber ?? invoiceId, outcome.RulesetId!) }
            : outcome;
    }

    /// <summary>
    /// Everything an export checks, for a draft that is not booked yet: the
    /// figures as booking would store them, the same format resolution, the
    /// runtime rules and the official ones. No trial rule (checking is free)
    /// and nothing is stored. Exported means it would export as it stands.
    /// </summary>
    /// <param name="invoiceId">The id it would be booked under (its document UUID derives from it).</param>
    public async Task<ExportOutcome> CheckAsBookedAsync(
        string organizationId,
        string invoiceId,
        CanonicalInvoiceDraft draft,
        string? requestedRulesetId,
        CancellationToken cancellationToken = default)
    {
        if (requestedRulesetId is not null && rulesets.Find(requestedRulesetId) is null)
        {
            return ExportOutcome.UnknownRuleset;
        }

        return await ProduceAsync(organizationId, invoiceId, InvoiceTotalsCalculator.Apply(draft), requestedRulesetId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Makes the file from an invoice as stored, and checks it completely.</summary>
    private async Task<ExportOutcome> ProduceAsync(
        string organizationId,
        string invoiceId,
        CanonicalInvoiceDraft invoice,
        string? requestedRulesetId,
        CancellationToken cancellationToken)
    {
        var enriched = await enrichment.EnrichAsync(organizationId, invoice, cancellationToken).ConfigureAwait(false);

        var organization = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var record = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var ruleset = rulesets.Resolve(
            requestedRulesetId,
            enriched.Relation?.DefaultRulesetId,
            organization?.DefaultRulesetId,
            Countries.Find(record?.CountryCode)?.DefaultRulesetId)!;

        var draft = enriched.Draft with
        {
            // Stable per stored invoice, so a re-export carries the same UUID.
            DocumentUuid = enriched.Draft.DocumentUuid ?? DocumentUuids.For(organizationId, invoiceId),
        };

        var result = ValidatedCanonicalInvoice.Validate(ruleset, draft);
        if (result.Invoice is null)
        {
            return new ExportOutcome(ExportOutcomeKind.Incomplete, Violations: result.Violations, RulesetId: ruleset.Id);
        }

        var xml = writer.Write(result.Invoice);

        // The runtime rules above are a subset; the official ones are complete.
        // No verdict means no file: an unchecked document is never handed out.
        var official = await officialValidator.ValidateAsync(ruleset.Id, xml, cancellationToken).ConfigureAwait(false);
        if (!official.Available)
        {
            return ExportOutcome.ValidationUnavailable with { RulesetId = ruleset.Id };
        }

        var fatal = official.Failures.Where(f => f.IsFatal).Select(ToViolation).ToList();
        if (fatal.Count > 0)
        {
            return new ExportOutcome(ExportOutcomeKind.Incomplete, Violations: fatal, RulesetId: ruleset.Id);
        }

        return new ExportOutcome(
            ExportOutcomeKind.Exported,
            Content: new UTF8Encoding(false).GetBytes(xml),
            Warnings: official.Failures.Where(f => !f.IsFatal).Select(ToViolation).ToList(),
            RulesetId: ruleset.Id);
    }

    /// <summary>An official failure in the shape the screen already shows; its path is a location in the document.</summary>
    private static RuleViolation ToViolation(OfficialFailure failure)
        => new(failure.RuleId, null, failure.Location, failure.Message);

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

    /// <summary>The official validator could not be asked; no file is handed out.</summary>
    ValidationUnavailable,

    /// <summary>The organisation is on a trial: checking is free, the file needs a plan.</summary>
    PlanRequired,

    /// <summary>The invoice is a saved concept; it is exported once booked.</summary>
    NotBooked,
}

/// <param name="Warnings">Official warnings for an exported file; they do not block it.</param>
/// <param name="RulesetId">The format that was checked, once one was chosen.</param>
public sealed record ExportOutcome(
    ExportOutcomeKind Kind,
    string? FileName = null,
    byte[]? Content = null,
    IReadOnlyList<RuleViolation>? Violations = null,
    IReadOnlyList<RuleViolation>? Warnings = null,
    string? RulesetId = null)
{
    public static readonly ExportOutcome NotFound = new(ExportOutcomeKind.NotFound);

    public static readonly ExportOutcome UnknownRuleset = new(ExportOutcomeKind.UnknownRuleset);

    public static readonly ExportOutcome ValidationUnavailable = new(ExportOutcomeKind.ValidationUnavailable);

    public static readonly ExportOutcome PlanRequired = new(ExportOutcomeKind.PlanRequired);

    public static readonly ExportOutcome NotBooked = new(ExportOutcomeKind.NotBooked);
}
