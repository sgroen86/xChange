using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// The extraction use case: interpret a PDF, check the arithmetic, and produce
/// the canonical XML.
///
/// The ordering matters and is the whole point of the pipeline in CLAUDE.md:
/// the model only ever produces the draft, and the XML is generated afterwards
/// by deterministic code from that draft. The provider is never asked for XML.
/// </summary>
public sealed class ExtractInvoiceService(
    IInvoiceInterpreter interpreter,
    InvoiceCalculationValidator validator,
    CanonicalInvoiceXmlSerializer serializer)
{
    public async Task<ExtractInvoiceResult> ExtractAsync(
        ReadOnlyMemory<byte> pdf,
        CancellationToken cancellationToken = default)
    {
        var interpretation = await interpreter.InterpretAsync(pdf, cancellationToken)
            .ConfigureAwait(false);

        var warnings = validator.Validate(interpretation.Draft);
        var xml = serializer.Serialize(interpretation.Draft);

        return new ExtractInvoiceResult(
            InvoiceId: Guid.NewGuid().ToString("n"),
            Draft: interpretation.Draft,
            CanonicalXml: xml,
            Warnings: warnings,
            Execution: interpretation.Execution);
    }
}

/// <param name="InvoiceId">Identifier for this extraction. Nothing is persisted yet.</param>
public sealed record ExtractInvoiceResult(
    string InvoiceId,
    CanonicalInvoiceDraft Draft,
    string CanonicalXml,
    IReadOnlyList<ValidationWarning> Warnings,
    ProviderExecution Execution);
