using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Relations;

public sealed record RelationResponse
{
    [JsonPropertyName("relationId")]
    public required string RelationId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("vatIdentifier")]
    public string? VatIdentifier { get; init; }

    [JsonPropertyName("legalRegistrationId")]
    public string? LegalRegistrationId { get; init; }

    [JsonPropertyName("legalRegistrationScheme")]
    public string? LegalRegistrationScheme { get; init; }

    [JsonPropertyName("electronicAddress")]
    public string? ElectronicAddress { get; init; }

    [JsonPropertyName("electronicAddressScheme")]
    public string? ElectronicAddressScheme { get; init; }

    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record UpdateRelationRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("vatIdentifier")]
    public string? VatIdentifier { get; init; }

    [JsonPropertyName("legalRegistrationId")]
    public string? LegalRegistrationId { get; init; }

    [JsonPropertyName("legalRegistrationScheme")]
    public string? LegalRegistrationScheme { get; init; }

    [JsonPropertyName("electronicAddress")]
    public string? ElectronicAddress { get; init; }

    [JsonPropertyName("electronicAddressScheme")]
    public string? ElectronicAddressScheme { get; init; }

    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; init; }
}
