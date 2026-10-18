using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Relations;

/// <summary>
/// How a party on an invoice is recognised as a known relation: by VAT number,
/// otherwise by registration (KvK) number. Never by name - "Jansen B.V." and
/// "Jansen Installatietechniek" are too easily confused, and a wrong match
/// would put someone else's PEPPOL id in the file.
/// </summary>
public static class RelationKeys
{
    /// <summary>"nl 8149.12.345-b01" -> "NL814912345B01". Null when nothing is left.</summary>
    public static string? NormalizeVat(string? vat)
    {
        if (string.IsNullOrWhiteSpace(vat))
        {
            return null;
        }

        var normalized = new string(vat.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>"1234 5678" -> "12345678": registration numbers compare on their digits.</summary>
    public static string? NormalizeRegistration(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var digits = new string(id.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    /// <summary>True when the party carries something a relation can be found by later.</summary>
    public static bool IsIdentifiable(CanonicalParty? party)
        => NormalizeVat(party?.VatIdentifier) is not null || NormalizeRegistration(party?.LegalRegistrationId) is not null;

    /// <summary>The relation with the party's VAT number, else with its registration number, else none.</summary>
    public static Relation? FindMatch(IEnumerable<Relation> relations, CanonicalParty? party)
    {
        if (party is null)
        {
            return null;
        }

        var candidates = relations as IReadOnlyCollection<Relation> ?? relations.ToList();

        var vat = NormalizeVat(party.VatIdentifier);
        if (vat is not null && candidates.FirstOrDefault(r => r.VatIdentifier == vat) is { } byVat)
        {
            return byVat;
        }

        var registration = NormalizeRegistration(party.LegalRegistrationId);
        return registration is null
            ? null
            : candidates.FirstOrDefault(r => NormalizeRegistration(r.LegalRegistrationId) == registration);
    }
}
