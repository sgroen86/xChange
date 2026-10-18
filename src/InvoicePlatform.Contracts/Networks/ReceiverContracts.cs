using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Networks;

/// <summary>The buyer's numbers, as the review screen has them. The name is deliberately absent: a buyer is never looked up by name.</summary>
public sealed record LookupReceiverRequest
{
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

public sealed record ReceiverLookupResponse
{
    /// <summary>"found", "notFound", "unavailable" or "nothingToLookUp".</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>"peppol" when found.</summary>
    [JsonPropertyName("network")]
    public string? Network { get; init; }

    [JsonPropertyName("participant")]
    public ParticipantInfo? Participant { get; init; }

    /// <summary>xChange formats the receiver takes invoices in.</summary>
    [JsonPropertyName("acceptedRulesetIds")]
    public IReadOnlyList<string> AcceptedRulesetIds { get; init; } = [];

    [JsonPropertyName("documentTypeCount")]
    public int DocumentTypeCount { get; init; }

    /// <summary>The addresses asked, "0106:12345678", in order of confidence.</summary>
    [JsonPropertyName("tried")]
    public IReadOnlyList<string> Tried { get; init; } = [];
}

public sealed record ParticipantInfo
{
    [JsonPropertyName("scheme")]
    public required string Scheme { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
