namespace InvoicePlatform.Domain.Relations;

/// <summary>
/// A trading partner the organisation invoices - today always a customer,
/// because xChange handles outgoing invoices. It keeps what PEPPOL needs and a
/// PDF rarely shows, so the second invoice to the same customer is complete
/// without retyping.
///
/// Tenant-owned: <see cref="OrganizationId"/> is its partition. A customer of
/// two xChange organisations is two relations; nothing is shared.
/// </summary>
public sealed record Relation
{
    public required string RelationId { get; init; }

    public required string OrganizationId { get; init; }

    public string? Name { get; init; }

    /// <summary>Normalised: upper case, no spaces, dots or dashes.</summary>
    public string? VatIdentifier { get; init; }

    /// <summary>Digits only for KvK; as entered otherwise.</summary>
    public string? LegalRegistrationId { get; init; }

    public string? LegalRegistrationScheme { get; init; }

    public string? ElectronicAddress { get; init; }

    public string? ElectronicAddressScheme { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
