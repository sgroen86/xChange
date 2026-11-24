namespace InvoicePlatform.Domain.Organizations;

/// <summary>
/// What an organisation tells xChange about itself as the seller on its
/// invoices. <see cref="General"/> is shared by every e-invoice format (they all
/// specialise EN 16931); a network or format that asks for more gets a block of
/// its own, PEPPOL first. The country is not here: it is the organisation's
/// (<see cref="Organization.CountryCode"/>).
/// </summary>
public sealed record OrganizationProfile
{
    public required string OrganizationId { get; init; }

    public CompanyDetails General { get; init; } = new();

    public PeppolDetails Peppol { get; init; } = new();
}

/// <summary>The seller's details every format uses. BT numbers per EN 16931.</summary>
public sealed record CompanyDetails
{
    /// <summary>BT-27.</summary>
    public string? LegalName { get; init; }

    /// <summary>BT-31.</summary>
    public string? VatIdentifier { get; init; }

    /// <summary>BT-30, with its ISO 6523 scheme BT-30-1: "0106" KvK, "0208" KBO.</summary>
    public string? LegalRegistrationId { get; init; }

    public string? LegalRegistrationScheme { get; init; }

    /// <summary>BT-35, BT-36, BT-38, BT-37.</summary>
    public string? StreetName { get; init; }

    public string? AdditionalStreetName { get; init; }

    public string? PostalZone { get; init; }

    public string? CityName { get; init; }

    /// <summary>BT-41, BT-43, BT-42.</summary>
    public string? ContactName { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    /// <summary>BT-81, UNCL 4461: the usual payment means, e.g. "30" or "58".</summary>
    public string? PaymentMeansCode { get; init; }

    /// <summary>BT-84, BT-86, BT-85.</summary>
    public string? Iban { get; init; }

    public string? Bic { get; init; }

    public string? AccountName { get; init; }
}

/// <summary>BT-34 and its EAS scheme: the seller's PEPPOL participant id.</summary>
public sealed record PeppolDetails
{
    public string? EndpointId { get; init; }

    public string? EndpointScheme { get; init; }
}
