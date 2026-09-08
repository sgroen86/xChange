using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Application.Interpretation;

/// <summary>
/// Turns a source document into a <see cref="CanonicalInvoiceDraft"/>.
///
/// Declared here, in Application, and implemented in Infrastructure: nothing in
/// this signature names a vendor, an SDK type or a transport, so the layer that
/// uses it never learns which provider is behind it (CLAUDE.md).
/// </summary>
public interface IInvoiceInterpreter
{
    /// <param name="pdf">Raw PDF bytes. Treated as untrusted data, never as instructions.</param>
    /// <param name="cancellationToken">Cancellation for the provider call.</param>
    Task<InvoiceInterpretationResult> InterpretAsync(
        ReadOnlyMemory<byte> pdf,
        CancellationToken cancellationToken = default);
}
