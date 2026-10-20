using System.Security.Cryptography;
using System.Text;

namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// Time-based one-time passwords, RFC 6238 with the parameters every
/// authenticator app defaults to: HMAC-SHA1, six digits, thirty seconds.
///
/// Written out rather than taken from a package: it is HMAC plus dynamic
/// truncation, and the RFC publishes test vectors, so it can be proven
/// correct rather than trusted. Pure: the caller supplies the time and the
/// last step that was used, and stores the step this returns.
/// </summary>
public static class Totp
{
    public const int Digits = 6;

    public const int PeriodSeconds = 30;

    /// <summary>One step either side, to absorb drift between phone and server.</summary>
    public const int AllowedDrift = 1;

    /// <summary>160 bits, the size of an HMAC-SHA1 key (RFC 4226 recommends it).</summary>
    public const int SecretBytes = 20;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static long StepAt(DateTimeOffset at) => at.ToUnixTimeSeconds() / PeriodSeconds;

    /// <summary>The code for one time step (RFC 4226 section 5.3).</summary>
    public static string CodeAt(byte[] secret, long step, int digits = Digits)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xff);
            step >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);

        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        var modulus = 1;
        for (var i = 0; i < digits; i++)
        {
            modulus *= 10;
        }

        return (binary % modulus).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    /// <summary>
    /// The step a code belongs to, or null when it matches none within the drift
    /// window - or only steps at or before <paramref name="lastUsedStep"/>, so a
    /// code that was already used is refused even while it is still current.
    /// </summary>
    public static long? Verify(byte[] secret, string? code, DateTimeOffset now, long? lastUsedStep)
    {
        var normalized = Normalize(code);
        if (normalized is null)
        {
            return null;
        }

        var current = StepAt(now);
        var given = Encoding.ASCII.GetBytes(normalized);
        long? matched = null;

        // Every candidate is computed and compared, so the time taken does not
        // reveal which step matched.
        for (var offset = -AllowedDrift; offset <= AllowedDrift; offset++)
        {
            var step = current + offset;
            var expected = Encoding.ASCII.GetBytes(CodeAt(secret, step));
            if (CryptographicOperations.FixedTimeEquals(expected, given)
                && (lastUsedStep is null || step > lastUsedStep)
                && matched is null)
            {
                matched = step;
            }
        }

        return matched;
    }

    /// <summary>Six digits, with any spaces a person typed or pasted removed; null when it cannot be a code.</summary>
    public static string? Normalize(string? code)
    {
        if (code is null)
        {
            return null;
        }

        var digits = new string(code.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        return digits.Length == Digits && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    /// <summary>
    /// The URI authenticator apps read from the QR code
    /// (https://github.com/google/google-authenticator/wiki/Key-Uri-Format).
    /// </summary>
    public static string OtpAuthUri(string issuer, string account, byte[] secret)
    {
        var label = $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}";
        return $"otpauth://totp/{label}?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}"
            + $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
    }
}

/// <summary>RFC 4648 base32 without padding, the form authenticator apps take a secret in.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    /// <summary>Case-insensitive; spaces, dashes and padding are ignored. Throws on any other character.</summary>
    public static byte[] Decode(string text)
    {
        var output = new List<byte>(text.Length * 5 / 8);
        int buffer = 0, bits = 0;

        foreach (var raw in text)
        {
            if (raw is ' ' or '-' or '=')
            {
                continue;
            }

            var value = Alphabet.IndexOf(char.ToUpperInvariant(raw));
            if (value < 0)
            {
                throw new FormatException($"'{raw}' is not a base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
