using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Invoices;

/// <summary>Where an invoice is in its short life.</summary>
public enum InvoiceStatus
{
    /// <summary>Extracted and reviewable, not yet committed to.</summary>
    Draft,

    /// <summary>The user has booked it: kept deliberately, not incidentally.</summary>
    Booked,
}

/// <summary>
/// Which way an invoice travels. Only outgoing invoices exist today: the
/// organisation uploads its own sales invoices. The field is stored now so
/// that received invoices, a later piece of work, fit the same store and list.
/// </summary>
public enum InvoiceDirection
{
    Outgoing,
}

/// <summary>
/// An invoice as it is kept.
///
/// <see cref="OrganizationId"/> is the tenant key required by CLAUDE.md, and it
/// is also the partition key in storage: an invoice belonging to another
/// organisation is not merely unauthorised, it is in a different partition.
///
/// The PDF is not here. <see cref="DocumentKey"/> points at object storage,
/// because a database is the wrong place for blobs.
/// </summary>
public sealed record StoredInvoice
{
    public required string InvoiceId { get; init; }

    public required string OrganizationId { get; init; }

    public required InvoiceStatus Status { get; init; }

    public InvoiceDirection Direction { get; init; } = InvoiceDirection.Outgoing;

    public required CanonicalInvoiceDraft Invoice { get; init; }

    /// <summary>Object-storage key of the source PDF. Null until one is stored.</summary>
    public string? DocumentKey { get; init; }

    public string? SourceFileName { get; init; }

    public long SourceByteSize { get; init; }

    /// <summary>Model that produced the extraction, for provenance.</summary>
    public string? ProviderModel { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string CreatedByUserId { get; init; }

    public DateTimeOffset? BookedAt { get; init; }

    public string? BookedByUserId { get; init; }
}
