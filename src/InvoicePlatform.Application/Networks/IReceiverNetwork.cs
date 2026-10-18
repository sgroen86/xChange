using InvoicePlatform.Domain.Networks;

namespace InvoicePlatform.Application.Networks;

/// <summary>
/// An e-invoicing network a receiver can be registered on. PEPPOL is the
/// first; another network is another implementation, not a change here.
/// </summary>
public interface IReceiverNetwork
{
    /// <summary>"peppol".</summary>
    string Name { get; }

    /// <summary>
    /// Whether the participant is registered, and which document types it
    /// accepts. Never throws for a network problem: that is
    /// <see cref="NetworkAnswerKind.Unavailable"/>.
    /// </summary>
    Task<NetworkAnswer> FindAsync(ParticipantIdentifier participant, CancellationToken cancellationToken = default);
}

public enum NetworkAnswerKind
{
    Registered,
    NotRegistered,

    /// <summary>The network could not be asked (timeout, DNS or HTTP failure).</summary>
    Unavailable,
}

public sealed record NetworkAnswer(NetworkAnswerKind Kind, IReadOnlyList<DocumentTypeId> DocumentTypes)
{
    public static readonly NetworkAnswer NotRegistered = new(NetworkAnswerKind.NotRegistered, []);

    public static readonly NetworkAnswer Unavailable = new(NetworkAnswerKind.Unavailable, []);

    public static NetworkAnswer Registered(IReadOnlyList<DocumentTypeId> documentTypes) => new(NetworkAnswerKind.Registered, documentTypes);
}
