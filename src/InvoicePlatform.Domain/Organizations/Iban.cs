using System.Text.RegularExpressions;

namespace InvoicePlatform.Domain.Organizations;

/// <summary>The ISO 13616 check: move the first four characters to the end, letters to numbers, mod 97 is 1.</summary>
public static partial class Iban
{
    /// <param name="iban">Compacted and upper case, as <see cref="OrganizationProfileRules.Normalize"/> leaves it.</param>
    public static bool IsValid(string? iban)
    {
        if (iban is null || !Shape().IsMatch(iban))
        {
            return false;
        }

        var remainder = 0;
        foreach (var c in iban[4..] + iban[..4])
        {
            remainder = char.IsAsciiDigit(c)
                ? ((remainder * 10) + (c - '0')) % 97
                : ((remainder * 100) + (c - 'A' + 10)) % 97;
        }

        return remainder == 1;
    }

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex Shape();
}
