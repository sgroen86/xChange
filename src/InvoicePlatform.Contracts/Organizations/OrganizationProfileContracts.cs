using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Organizations;

/// <summary>The seller's details every e-invoice format uses: the profile's General tab.</summary>
public sealed record CompanyDetailsDto
{
    [JsonPropertyName("legalName")]
    public string? LegalName { get; init; }

    [JsonPropertyName("vatIdentifier")]
    public string? VatIdentifier { get; init; }

    [JsonPropertyName("legalRegistrationId")]
    public string? LegalRegistrationId { get; init; }

    [JsonPropertyName("legalRegistrationScheme")]
    public string? LegalRegistrationScheme { get; init; }

    [JsonPropertyName("streetName")]
    public string? StreetName { get; init; }

    [JsonPropertyName("additionalStreetName")]
    public string? AdditionalStreetName { get; init; }

    [JsonPropertyName("postalZone")]
    public string? PostalZone { get; init; }

    [JsonPropertyName("cityName")]
    public string? CityName { get; init; }

    [JsonPropertyName("contactName")]
    public string? ContactName { get; init; }

    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    [JsonPropertyName("contactPhone")]
    public string? ContactPhone { get; init; }

    [JsonPropertyName("paymentMeansCode")]
    public string? PaymentMeansCode { get; init; }

    [JsonPropertyName("iban")]
    public string? Iban { get; init; }

    [JsonPropertyName("bic")]
    public string? Bic { get; init; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; init; }
}

/// <summary>The PEPPOL tab.</summary>
public sealed record PeppolDetailsDto
{
    [JsonPropertyName("endpointId")]
    public string? EndpointId { get; init; }

    [JsonPropertyName("endpointScheme")]
    public string? EndpointScheme { get; init; }
}

public sealed record OrganizationProfileResponse
{
    /// <summary>Read-only: the name the organisation was created with; the screen offers it as the legal name.</summary>
    [JsonPropertyName("organizationName")]
    public string? OrganizationName { get; init; }

    /// <summary>Read-only: only the platform administrator changes the country.</summary>
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; init; }

    [JsonPropertyName("general")]
    public required CompanyDetailsDto General { get; init; }

    [JsonPropertyName("peppol")]
    public required PeppolDetailsDto Peppol { get; init; }
}

public sealed record UpdateOrganizationProfileRequest
{
    [JsonPropertyName("general")]
    public CompanyDetailsDto? General { get; init; }

    [JsonPropertyName("peppol")]
    public PeppolDetailsDto? Peppol { get; init; }
}
