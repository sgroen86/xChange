using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// The extraction use case: interpret a PDF, fill what the organisation already
/// told us, check the arithmetic, and produce the canonical XML.
///
/// The ordering matters and is the whole point of the pipeline in CLAUDE.md:
/// the model only ever produces the draft, and the XML is generated afterwards
/// by deterministic code from that draft. The provider is never asked for XML.
/// </summary>
public sealed class ExtractInvoiceService(
    IInvoiceInterpreter interpreter,
    InvoiceEnrichment enrichment,
    InvoiceCalculationValidator validator,
    CanonicalInvoiceXmlSerializer serializer,
    LearningService learning,
    IPlatformStore platform,
    IUsageMeter usage,
    IUsageLedger ledger,
    IClock clock,
    ILogger<ExtractInvoiceService> logger)
{
    /// <param name="actor">Who the model call is booked to: the user id, or <see cref="UsageActors.Email"/>.</param>
    /// <param name="source">One of <see cref="UsageSources"/>.</param>
    public async Task<ExtractInvoiceResult> ExtractAsync(
        string organizationId,
        ReadOnlyMemory<byte> pdf,
        string actor,
        string source,
        CancellationToken cancellationToken = default)
    {
        // One conversion per PDF read, counted before the model is asked and
        // given back if reading fails: a trial stops at its limit, not after it.
        var bucket = await usage.TryConsumeAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new ConversionLimitReachedException();

        try
        {
            return await ReadAsync(organizationId, pdf, actor, source, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Back to where it came from: a failed read never costs a purchased credit.
            await usage.RefundAsync(organizationId, bucket, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<ExtractInvoiceResult> ReadAsync(
        string organizationId,
        ReadOnlyMemory<byte> pdf,
        string actor,
        string source,
        CancellationToken cancellationToken)
    {
        var extractionId = Guid.NewGuid().ToString("n");
        var hints = await learning.HintsAsync(organizationId, cancellationToken).ConfigureAwait(false);

        // The platform administrator's choice of model; none set means the configured default.
        var model = (await platform.GetSettingsAsync(cancellationToken).ConfigureAwait(false)).ExtractionModel;

        InvoiceInterpretationResult interpretation;
        try
        {
            interpretation = await interpreter.InterpretAsync(pdf, hints, model, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InterpretationFailedException failed) when (failed.Execution is not null)
        {
            // The model answered and was paid, even though the answer is no use.
            await RecordAsync(organizationId, actor, source, extractionId, failed.Execution, succeeded: false).ConfigureAwait(false);
            throw;
        }

        await RecordAsync(organizationId, actor, source, extractionId, interpretation.Execution, succeeded: true).ConfigureAwait(false);

        // What reviewers corrected before goes back in first; the enrichment
        // then only fills what is still empty.
        var learned = await learning.ApplyAsync(organizationId, interpretation.Draft, cancellationToken)
            .ConfigureAwait(false);

        var enriched = await enrichment.EnrichAsync(organizationId, learned.Draft, cancellationToken)
            .ConfigureAwait(false);

        // The model's own reading and what the reviewer will see, for booking
        // to learn from and to measure the reading against.
        await learning.KeepExtractionAsync(
            organizationId, extractionId, interpretation.Execution.Model, interpretation.Draft, learned, enriched.Draft, cancellationToken)
            .ConfigureAwait(false);

        var warnings = learned.Notices.Concat(enriched.Notices).Concat(validator.Validate(enriched.Draft)).ToList();
        var xml = serializer.Serialize(enriched.Draft);

        return new ExtractInvoiceResult(
            InvoiceId: extractionId,
            Draft: enriched.Draft,
            CanonicalXml: xml,
            Warnings: warnings,
            Execution: interpretation.Execution);
    }

    /// <summary>
    /// Books the model call to the ledger. Never fails the extraction: the
    /// customer's invoice matters more than our bookkeeping, so a failure here
    /// is logged and the reading goes on.
    /// </summary>
    private async Task RecordAsync(
        string organizationId,
        string actor,
        string source,
        string extractionId,
        ProviderExecution execution,
        bool succeeded)
    {
        try
        {
            var cost = ModelPrices.Cost(execution.Model, execution.Usage);
            if (!cost.PriceKnown)
            {
                logger.LogWarning("No price for model {Model} in price list {PriceVersion}; its calls are recorded at 0", execution.Model, cost.PriceVersion);
            }

            // Not cancelled with the request: the call is paid for either way.
            await ledger.RecordAsync(
                new UsageEvent
                {
                    EventId = Guid.NewGuid().ToString("n"),
                    OrganizationId = organizationId,
                    Actor = actor,
                    Source = source,
                    ExtractionId = extractionId,
                    Model = execution.Model,
                    Tokens = execution.Usage,
                    CostUsd = cost.CostUsd,
                    PriceVersion = cost.PriceVersion,
                    PriceKnown = cost.PriceKnown,
                    Succeeded = succeeded,
                    At = clock.UtcNow,
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Recording the cost of extraction {ExtractionId} failed; the extraction goes on", extractionId);
        }
    }
}

/// <param name="InvoiceId">
/// Identifier for this extraction. Only the model's reading is kept under it
/// (for learning, 30 days); booking passes it back as the extraction id.
/// </param>
public sealed record ExtractInvoiceResult(
    string InvoiceId,
    CanonicalInvoiceDraft Draft,
    string CanonicalXml,
    IReadOnlyList<ValidationWarning> Warnings,
    ProviderExecution Execution);
