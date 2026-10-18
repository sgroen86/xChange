using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Domain.Networks;

/// <summary>
/// The PEPPOL addresses a party could be registered under, derived from what
/// the invoice says about it - never from its name. In order of confidence:
/// the electronic address the invoice gives, then the VAT number, then the
/// registration number, each in the scheme PEPPOL uses for it.
/// </summary>
public static class PeppolIdentifiers
{
    /// <summary>EAS codes for a VAT number by its country prefix; the value is the whole number.</summary>
    private static readonly IReadOnlyDictionary<string, string> VatSchemes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["NL"] = "9944",
        ["BE"] = "9925",
        ["GB"] = "9932",
    };

    public static IReadOnlyList<ParticipantIdentifier> CandidatesFor(CanonicalParty? party)
    {
        if (party is null)
        {
            return [];
        }

        var candidates = new List<ParticipantIdentifier>();
        var country = party.Address?.CountryCode?.Trim().ToUpperInvariant();

        var endpoint = party.ElectronicAddress?.Trim();
        var endpointScheme = party.ElectronicAddressScheme?.Trim();
        if (!string.IsNullOrEmpty(endpoint) && CodeLists.IsElectronicAddressScheme(endpointScheme))
        {
            candidates.Add(new(endpointScheme!, endpoint));
        }

        var vat = RelationKeys.NormalizeVat(party.VatIdentifier);
        if (vat is { Length: > 2 } && VatSchemes.TryGetValue(vat[..2], out var vatScheme))
        {
            candidates.Add(new(vatScheme, vat));

            // A Belgian VAT number is BE + the enterprise number, which is also 0208.
            if (vat.StartsWith("BE", StringComparison.Ordinal) && vat.Length == 12)
            {
                candidates.Add(new("0208", vat[2..]));
            }
        }

        Registration(party, country, candidates);

        return candidates
            .DistinctBy(c => (c.Scheme, c.Value.ToLowerInvariant()))
            .ToList();
    }

    private static void Registration(CanonicalParty party, string? country, List<ParticipantIdentifier> candidates)
    {
        var id = party.LegalRegistrationId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        var digits = RelationKeys.NormalizeRegistration(id);
        var scheme = party.LegalRegistrationScheme?.Trim();

        switch (scheme)
        {
            case "0106" or "0208" when digits is not null:
                candidates.Add(new(scheme, digits));
                return;
            case "0195":
                candidates.Add(new(scheme, Uen(id)));
                return;
            case { Length: > 0 } when CodeLists.IsElectronicAddressScheme(scheme):
                candidates.Add(new(scheme, id));
                return;
        }

        // No scheme given: infer it only where the country makes it unambiguous.
        switch (country)
        {
            case "NL" when digits is { Length: 8 }:
                candidates.Add(new("0106", digits));
                break;
            case "BE" when digits is { Length: 10 }:
                candidates.Add(new("0208", digits));
                break;
            case "SG":
                candidates.Add(new("0195", Uen(id)));
                break;
        }
    }

    /// <summary>A Singapore UEN on PEPPOL is "SGUEN" + the UEN.</summary>
    private static string Uen(string id)
    {
        var compact = new string(id.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return compact.StartsWith("SGUEN", StringComparison.Ordinal) ? compact : "SGUEN" + compact;
    }
}
