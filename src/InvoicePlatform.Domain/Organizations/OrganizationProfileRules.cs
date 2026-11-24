using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Domain.Organizations;

/// <param name="Field">Dotted path in the profile, e.g. "general.vatIdentifier"; the profile screen's input ids.</param>
public sealed record ProfileFieldError(string Field, string Message);

/// <summary>
/// Cleaning and checking a company profile before it is stored. Deterministic.
/// Every field is optional: an empty profile is valid and changes nothing.
/// </summary>
public static class OrganizationProfileRules
{
    /// <summary>Countries whose VAT number carries the country code as prefix and has a known shape.</summary>
    private static readonly string[] PrefixedCountries = ["NL", "BE", "GB"];

    /// <summary>Trimmed, empty as null, VAT and IBAN compacted, KvK and KBO as digits, a scheme without its value dropped.</summary>
    public static OrganizationProfile Normalize(OrganizationProfile profile)
    {
        var general = profile.General;
        var scheme = Clean(general.LegalRegistrationScheme);
        var registration = Clean(general.LegalRegistrationId);
        if (scheme is "0106" or "0208")
        {
            registration = RelationKeys.NormalizeRegistration(registration);
        }

        var endpoint = Clean(profile.Peppol.EndpointId);

        return profile with
        {
            General = new CompanyDetails
            {
                LegalName = Clean(general.LegalName),
                VatIdentifier = RelationKeys.NormalizeVat(general.VatIdentifier),
                LegalRegistrationId = registration,
                LegalRegistrationScheme = registration is null ? null : scheme,
                StreetName = Clean(general.StreetName),
                AdditionalStreetName = Clean(general.AdditionalStreetName),
                PostalZone = Clean(general.PostalZone),
                CityName = Clean(general.CityName),
                ContactName = Clean(general.ContactName),
                ContactEmail = Clean(general.ContactEmail),
                ContactPhone = Clean(general.ContactPhone),
                PaymentMeansCode = Clean(general.PaymentMeansCode),
                Iban = Compact(general.Iban),
                Bic = Compact(general.Bic),
                AccountName = Clean(general.AccountName),
            },
            Peppol = new PeppolDetails
            {
                EndpointId = endpoint,
                EndpointScheme = endpoint is null ? null : Clean(profile.Peppol.EndpointScheme),
            },
        };
    }

    /// <param name="profile">A profile as <see cref="Normalize"/> returns it.</param>
    /// <param name="countryCode">The organisation's country: a VAT number from NL, BE or GB must carry its prefix.</param>
    public static IReadOnlyList<ProfileFieldError> Check(OrganizationProfile profile, string? countryCode)
    {
        var errors = new List<ProfileFieldError>();
        var text = ProfileRuleText.Current;
        var general = profile.General;

        if (general.VatIdentifier is { } vat)
        {
            var country = countryCode?.Trim().ToUpperInvariant();
            if (country is not null && PrefixedCountries.Contains(country) && !vat.StartsWith(country, StringComparison.Ordinal))
            {
                errors.Add(new("general.vatIdentifier", text.VatPrefix(country)));
            }
            else if (!VatNumberShapes.Fits(vat))
            {
                errors.Add(new("general.vatIdentifier", text.VatShape(vat)));
            }
        }

        if (general.LegalRegistrationId is { } registration)
        {
            switch (general.LegalRegistrationScheme)
            {
                case null:
                    errors.Add(new("general.legalRegistrationScheme", text.RegistrationSchemeMissing));
                    break;
                case "0106" when registration.Length != 8:
                    errors.Add(new("general.legalRegistrationId", text.KvkShape));
                    break;
                case "0208" when registration.Length != 10 || !VatNumberShapes.Mod97(registration):
                    errors.Add(new("general.legalRegistrationId", text.KboShape));
                    break;
                case { } scheme when !CodeLists.IsRegistrationScheme(scheme):
                    errors.Add(new("general.legalRegistrationScheme", text.RegistrationSchemeInvalid(scheme)));
                    break;
            }
        }

        if (general.PaymentMeansCode is { } means && !CodeLists.IsPaymentMeansCode(means))
        {
            errors.Add(new("general.paymentMeansCode", text.PaymentMeansInvalid(means)));
        }

        if (general.Iban is { } iban && !Iban.IsValid(iban))
        {
            errors.Add(new("general.iban", text.IbanInvalid));
        }

        if (profile.Peppol.EndpointId is not null)
        {
            if (profile.Peppol.EndpointScheme is not { } eas)
            {
                errors.Add(new("peppol.endpointScheme", text.EndpointSchemeMissing));
            }
            else if (!CodeLists.IsElectronicAddressScheme(eas))
            {
                errors.Add(new("peppol.endpointScheme", text.EndpointSchemeInvalid(eas)));
            }
        }

        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Compact(string? value)
        => Clean(value) is { } clean ? new string(clean.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant() : null;
}
