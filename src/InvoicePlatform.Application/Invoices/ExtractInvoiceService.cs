using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

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
    LearningService learning)
{
    public async Task<ExtractInvoiceResult> ExtractAsync(
        string organizationId,
        ReadOnlyMemory<byte> pdf,
        CancellationToken cancellationToken = default)
    {
        var extractionId = Guid.NewGuid().ToString("n");
        var hints = await learning.HintsAsync(organizationId, cancellationToken).ConfigureAwait(false);

        var interpretation = await interpreter.InterpretAsync(pdf, hints, cancellationToken)
            .ConfigureAwait(false);

        // What reviewers corrected before goes back in first; the enrichment
        // then only fills what is still empty.
        var learned = await learning.ApplyAsync(organizationId, extractionId, interpretation.Draft, cancellationToken)
            .ConfigureAwait(false);

        var enriched = await enrichment.EnrichAsync(organizationId, learned.Draft, cancellationToken)
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
