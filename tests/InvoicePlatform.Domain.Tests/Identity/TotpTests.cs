using System.Text;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Domain.Tests.Identity;

/// <summary>
/// The TOTP core against RFC 6238's own test vectors (appendix B, SHA-1), so it
/// is proven rather than trusted, plus the window and replay rules xChange adds.
/// </summary>
public class TotpTests
{
    /// <summary>The RFC's SHA-1 seed: the ASCII string "12345678901234567890".</summary>
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void Codes_match_the_rfc_6238_test_vectors(long unixSeconds, string expected)
    {
        var step = Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, Totp.CodeAt(RfcSecret, step, digits: 8));
        // Six digits are the last six of the same number.
        Assert.Equal(expected[2..], Totp.CodeAt(RfcSecret, step));
    }

    [Fact]
    public void A_code_is_accepted_one_step_either_side_and_not_further()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111);
        var step = Totp.StepAt(now);

        Assert.Equal(step - 1, Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step - 1), now, null));
        Assert.Equal(step, Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step), now, null));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step + 1), now, null));
        Assert.Null(Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step - 2), now, null));
        Assert.Null(Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step + 2), now, null));
    }

    [Fact]
    public void A_used_step_or_an_earlier_one_is_refused()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var step = Totp.StepAt(now);
        var code = Totp.CodeAt(RfcSecret, step);

        Assert.Null(Totp.Verify(RfcSecret, code, now, lastUsedStep: step));
        Assert.Null(Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step - 1), now, lastUsedStep: step));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step + 1), now, lastUsedStep: step));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Anything_but_six_digits_is_refused(string? code)
        => Assert.Null(Totp.Verify(RfcSecret, code, DateTimeOffset.UnixEpoch, null));

    [Fact]
    public void Spaces_people_type_are_ignored()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(59);
        var code = Totp.CodeAt(RfcSecret, Totp.StepAt(now));

        Assert.NotNull(Totp.Verify(RfcSecret, $" {code[..3]} {code[3..]} ", now, null));
    }

    [Fact]
    public void Base32_follows_rfc_4648()
    {
        // RFC 4648 section 10, without padding.
        Assert.Equal("", Base32.Encode(""u8));
        Assert.Equal("MY", Base32.Encode("f"u8));
        Assert.Equal("MZXQ", Base32.Encode("fo"u8));
        Assert.Equal("MZXW6", Base32.Encode("foo"u8));
        Assert.Equal("MZXW6YQ", Base32.Encode("foob"u8));
        Assert.Equal("MZXW6YTB", Base32.Encode("fooba"u8));
        Assert.Equal("MZXW6YTBOI", Base32.Encode("foobar"u8));

        Assert.Equal("foobar"u8.ToArray(), Base32.Decode("mzxw 6ytb oi======"));
        Assert.Throws<FormatException>(() => Base32.Decode("MZ1W"));
    }

    [Fact]
    public void A_new_secret_is_160_bits_and_round_trips_through_base32()
    {
        var secret = Totp.NewSecret();

        Assert.Equal(20, secret.Length);
        Assert.Equal(32, Base32.Encode(secret).Length);
        Assert.Equal(secret, Base32.Decode(Base32.Encode(secret)));
        Assert.NotEqual(secret, Totp.NewSecret());
    }

    [Fact]
    public void The_otpauth_uri_carries_issuer_account_and_secret()
    {
        var uri = Totp.OtpAuthUri("xChange", "anna@bakkerij.be", RfcSecret);

        Assert.Equal(
            "otpauth://totp/xChange:anna%40bakkerij.be?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=xChange&algorithm=SHA1&digits=6&period=30",
            uri);
    }
}

public class RecoveryCodeTests
{
    [Fact]
    public void Ten_distinct_codes_in_a_readable_shape()
    {
        var codes = RecoveryCodes.Generate();

        Assert.Equal(RecoveryCodes.Count, codes.Count);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z2-7]{4}-[a-z2-7]{4}-[a-z2-7]{4}$", code));
    }

    [Fact]
    public void The_hash_ignores_case_spaces_and_dashes_and_is_not_the_code()
    {
        var hash = RecoveryCodes.HashOf("k7qm-x2ab-9fde");

        Assert.Equal(hash, RecoveryCodes.HashOf(" K7QM X2AB 9FDE "));
        Assert.Equal(hash, RecoveryCodes.HashOf("k7qmx2ab9fde"));
        Assert.DoesNotContain("k7qm", hash, StringComparison.Ordinal);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void Mfa_is_mandatory_for_admins_and_platform_administrators_only()
    {
        User Make(UserRole role, bool platform = false) => new()
        {
            Id = "u", OrganizationId = "o", Email = "e", PasswordHash = "-", Name = "n", Role = role, IsPlatformAdmin = platform,
        };

        Assert.True(UserMfa.IsMandatoryFor(Make(UserRole.Admin)));
        Assert.True(UserMfa.IsMandatoryFor(Make(UserRole.User, platform: true)));
        Assert.False(UserMfa.IsMandatoryFor(Make(UserRole.User)));
        Assert.False(UserMfa.IsMandatoryFor(Make(UserRole.ReadOnly)));
    }
}
