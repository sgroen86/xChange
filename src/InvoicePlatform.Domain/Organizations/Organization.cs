namespace InvoicePlatform.Domain.Organizations;

/// <summary>
/// An organisation as the platform knows it: a name and the country it is
/// established in. Its invoices, relations and settings live in its own
/// partition and are not part of this record.
/// </summary>
public sealed record Organization
{
    public required string OrganizationId { get; init; }

    public required string Name { get; init; }

    /// <summary>ISO 3166-1 alpha-2, one of <see cref="Countries.All"/>.</summary>
    public required string CountryCode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public string? CreatedByUserId { get; init; }

    /// <summary>Hash of the open invitation for the first user, if any; replaced when re-issued.</summary>
    public string? PendingInvitationHash { get; init; }

    public string? PendingInvitationEmail { get; init; }

    public DateTimeOffset? PendingInvitationExpiresAt { get; init; }
}
