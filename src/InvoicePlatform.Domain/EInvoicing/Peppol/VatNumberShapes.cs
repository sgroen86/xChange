using System.Text.RegularExpressions;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Peppol;

/// <summary>
/// The shape of Dutch, Belgian and British VAT numbers. BIS 3.0 itself only
/// checks the country prefix (BR-CO-09), so a mistyped number passes and is
/// refused by the receiver; these xChange rules (XC-*, not official) catch it
/// while the invoice is reviewed. A number is held to a country's shape only
/// when it starts with that country's prefix.
/// </summary>
internal static partial class VatNumberShapes
{
    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();
        var text = PeppolRuleText.Current;

        Check(invoice.Seller?.VatIdentifier, "seller.vatIdentifier", "BT-31", PartyRole.Seller, findings, text);
        Check(invoice.Buyer?.VatIdentifier, "buyer.vatIdentifier", "BT-48", PartyRole.Buyer, findings, text);

        return findings.Violations;
    }

    private static void Check(string? vat, string path, string businessTerm, PartyRole role, Findings findings, IPeppolRuleText text)
    {
        if (Findings.Missing(vat))
        {
            return;
        }

        var value = vat!.Trim();
        switch (value[..Math.Min(2, value.Length)])
        {
            case "NL" when !Dutch().IsMatch(value):
                findings.Add("XC-VAT-NL", businessTerm, path, text.DutchVatShape(role, value));
                break;

            case "BE" when !Belgian().IsMatch(value) || !Mod97(value[2..]):
                findings.Add("XC-VAT-BE", businessTerm, path, text.BelgianVatShape(role, value));
                break;

            case "GB" when !British().IsMatch(value):
                findings.Add("XC-VAT-GB", businessTerm, path, text.BritishVatShape(role, value));
                break;
        }
    }

    /// <summary>The Belgian enterprise-number check, as PEPPOL-COMMON-R043 does it for 0208.</summary>
    private static bool Mod97(string digits)
        => 97 - (int.Parse(digits[..8], System.Globalization.CultureInfo.InvariantCulture) % 97)
            == int.Parse(digits[8..], System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex("^NL[0-9]{9}B[0-9]{2}$")]
    private static partial Regex Dutch();

    [GeneratedRegex("^BE[01][0-9]{9}$")]
    private static partial Regex Belgian();

    [GeneratedRegex("^GB([0-9]{9}|[0-9]{12}|GD[0-4][0-9]{2}|HA[5-9][0-9]{2})$")]
    private static partial Regex British();
}
