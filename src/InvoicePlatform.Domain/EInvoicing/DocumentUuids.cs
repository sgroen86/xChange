using System.Security.Cryptography;
using System.Text;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The document UUID some rulesets require (PINT-SG BT-SG-003), derived from
/// the stored invoice so it is the same every time that invoice is exported.
/// </summary>
public static class DocumentUuids
{
    /// <summary>
    /// A name-based UUID (version 5, SHA-1) of organisation and invoice id:
    /// deterministic, in the standard 8-4-4-4-12 form, and different for the
    /// same invoice id in two organisations.
    /// </summary>
    public static string For(string organizationId, string invoiceId)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes($"xchange:{organizationId}/{invoiceId}"));
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant

        return string.Concat(
            Convert.ToHexString(bytes, 0, 4), "-",
            Convert.ToHexString(bytes, 4, 2), "-",
            Convert.ToHexString(bytes, 6, 2), "-",
            Convert.ToHexString(bytes, 8, 2), "-",
            Convert.ToHexString(bytes, 10, 6)).ToLowerInvariant();
    }
}
