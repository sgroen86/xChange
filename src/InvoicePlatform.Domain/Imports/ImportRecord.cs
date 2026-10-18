using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Domain.Imports;

public enum ImportStatus
{
    /// <summary>
    /// Received by email and waiting for a user to approve it. Not on the
    /// queue: nothing is spent on a document nobody asked for.
    /// </summary>
    AwaitingApproval,

    /// <summary>Stored and on the queue, not yet read.</summary>
    Queued,

    /// <summary>The worker is reading it.</summary>
    Processing,

    /// <summary>Read; waiting for a reviewer.</summary>
    Ready,

    /// <summary>Reading failed; <see cref="ImportRecord.Error"/> says why. Can be retried.</summary>
    Failed,

    /// <summary>
    /// Off the review list - booked, or dismissed - but kept, because it is a
    /// conversion its sender may still ask about. Only <see cref="ImportSource.Api"/>
    /// imports reach it; the others are deleted when they leave the list.
    /// </summary>
    Closed,
}

public enum ImportSource
{
    Upload,
    Email,

    /// <summary>Sent to the conversion API with an organisation's API key.</summary>
    Api,
}

/// <summary>
/// A PDF waiting to be read and reviewed - uploaded in a batch, or (later)
/// received by email. Read in the background by the worker, so a batch does
/// not wait on the model and a large attachment is not bound by the API's
/// request limit.
///
/// Tenant-owned: <see cref="OrganizationId"/> is the partition, as for
/// invoices. The import id becomes the invoice id when it is booked, so the
/// PDF is stored once, under the same key.
/// </summary>
public sealed record ImportRecord
{
    public required string ImportId { get; init; }

    public required string OrganizationId { get; init; }

    public required ImportSource Source { get; init; }

    public required ImportStatus Status { get; init; }

    public required string FileName { get; init; }

    public long ByteSize { get; init; }

    public required string DocumentKey { get; init; }

    /// <summary>For an email: who sent it, and its subject.</summary>
    public string? Sender { get; init; }

    public string? Subject { get; init; }

    /// <summary>Set once read: the extraction (for learning), the draft and its notices.</summary>
    public string? ExtractionId { get; init; }

    public CanonicalInvoiceDraft? Draft { get; init; }

    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = [];

    public string? ProviderModel { get; init; }

    public string? Error { get; init; }

    /// <summary>
    /// For a conversion: the format the file is made in. What the sender asked
    /// for, or - when they asked for none - the format chosen when it was
    /// checked, so the file is made in the format that was checked.
    /// </summary>
    public string? RulesetId { get; init; }

    /// <summary>For a conversion that could not be finished without a person: what stopped it.</summary>
    public IReadOnlyList<RuleViolation> Violations { get; init; } = [];

    public required DateTimeOffset CreatedAt { get; init; }

    public string? CreatedByUserId { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
