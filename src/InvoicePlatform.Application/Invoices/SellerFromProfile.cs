using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Relations;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// The seller from the company profile. Fixed details (name, numbers, address,
/// country, PEPPOL id, bank account) win over what the model read when a PDF
/// is read; contact and payment means are defaults, filled only where the
/// document had nothing. Deterministic - no model is involved.
/// </summary>
internal static class SellerFromProfile
{
    public const string Code = "ENRICH-PROFILE";

    public static CanonicalInvoiceDraft Apply(
        CanonicalInvoiceDraft draft,
        SellerProfile source,
        SellerFill fill,
        List<ValidationWarning> notices)
    {
        var profile = source.Profile.General;
        var peppol = source.Profile.Peppol;
        var filler = new Filler(fill, notices);

        var original = draft.Seller ?? new CanonicalParty();
        var originalAddress = original.Address ?? new CanonicalAddress();
        var originalPayment = draft.Payment ?? new CanonicalPaymentDetails();

        var seller = original with
        {
            Name = filler.Fixed("seller.name", original.Name, profile.LegalName, Loose),
            VatIdentifier = filler.Fixed("seller.vatIdentifier", original.VatIdentifier, profile.VatIdentifier, RelationKeys.NormalizeVat),
            ContactName = filler.Default("seller.contactName", original.ContactName, profile.ContactName),
            ContactEmail = filler.Default("seller.contactEmail", original.ContactEmail, profile.ContactEmail),
            ContactPhone = filler.Default("seller.contactPhone", original.ContactPhone, profile.ContactPhone),
        };

        // A number and its scheme travel together.
        if (profile.LegalRegistrationId is { } registration
            && filler.Fixed("seller.legalRegistrationId", original.LegalRegistrationId, registration, RelationKeys.NormalizeRegistration) == registration)
        {
            seller = seller with { LegalRegistrationId = registration, LegalRegistrationScheme = profile.LegalRegistrationScheme };
        }

        if (peppol.EndpointId is { } endpoint
            && filler.Fixed("seller.electronicAddress", original.ElectronicAddress, endpoint, Compact) == endpoint)
        {
            seller = seller with { ElectronicAddress = endpoint, ElectronicAddressScheme = peppol.EndpointScheme };
        }

        var address = originalAddress with
        {
            StreetName = filler.Fixed("seller.address.streetName", originalAddress.StreetName, profile.StreetName, Loose),
            AdditionalStreetName = filler.Fixed("seller.address.additionalStreetName", originalAddress.AdditionalStreetName, profile.AdditionalStreetName, Loose),
            PostalZone = filler.Fixed("seller.address.postalZone", originalAddress.PostalZone, profile.PostalZone, Compact),
            CityName = filler.Fixed("seller.address.cityName", originalAddress.CityName, profile.CityName, Loose),
            CountryCode = filler.Fixed("seller.address.countryCode", originalAddress.CountryCode, source.CountryCode, Loose),
        };
        if (address != originalAddress)
        {
            seller = seller with { Address = address };
        }

        var payment = originalPayment with
        {
            PaymentMeansCode = filler.Default("payment.paymentMeansCode", originalPayment.PaymentMeansCode, profile.PaymentMeansCode),
            Iban = filler.Fixed("payment.iban", originalPayment.Iban, profile.Iban, Compact),
            Bic = filler.Fixed("payment.bic", originalPayment.Bic, profile.Bic, Compact),
            AccountName = filler.Fixed("payment.accountName", originalPayment.AccountName, profile.AccountName, Loose),
        };

        // Untouched parts stay exactly as they were, null included.
        return draft with
        {
            Seller = seller == original ? draft.Seller : seller,
            Payment = payment == originalPayment ? draft.Payment : payment,
        };
    }

    /// <summary>Case and spacing do not make a different value.</summary>
    private static string? Loose(string? value)
        => value is null ? null : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string? Compact(string? value)
        => value is null ? null : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private sealed class Filler(SellerFill fill, List<ValidationWarning> notices)
    {
        private readonly IEnrichmentText _text = EnrichmentText.Current;

        /// <summary>A detail that is the same on every invoice.</summary>
        /// <returns>The profile's value when the field was empty, held the same value, or the profile wins; else the document's.</returns>
        public string? Fixed(string path, string? current, string? fromProfile, Func<string?, string?> normalize)
        {
            if (fromProfile is null)
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                notices.Add(new(Code, path, _text.SellerFilledFromProfile(_text.SellerField(path)), ValidationSeverity.Info));
                return fromProfile;
            }

            if (normalize(current) == normalize(fromProfile))
            {
                return fromProfile;
            }

            if (fill == SellerFill.OnlyEmpty)
            {
                return current;
            }

            notices.Add(new(Code, path, _text.SellerReplacedFromProfile(_text.SellerField(path), current.Trim()), ValidationSeverity.Warning));
            return fromProfile;
        }

        /// <summary>A default: used only where the document had nothing.</summary>
        public string? Default(string path, string? current, string? fromProfile)
        {
            if (fromProfile is null || !string.IsNullOrWhiteSpace(current))
            {
                return current;
            }

            notices.Add(new(Code, path, _text.SellerFilledFromProfile(_text.SellerField(path)), ValidationSeverity.Info));
            return fromProfile;
        }
    }
}
