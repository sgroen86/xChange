using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Domain.Tests.Organizations;

public class OrganizationTests
{
    [Theory]
    [InlineData("SG", true)]
    [InlineData("gb", true)]
    [InlineData(" NL ", true)]
    [InlineData("BE", true)]
    [InlineData("UK", false)] // ISO and PEPPOL say GB
    [InlineData("DE", false)]
    [InlineData(null, false)]
    public void Only_the_served_countries_are_supported(string? code, bool supported)
        => Assert.Equal(supported, Countries.IsSupported(code));

    [Fact]
    public void Invitation_codes_are_long_random_and_stored_only_as_a_hash()
    {
        var first = Invitation.NewCode();
        var second = Invitation.NewCode();

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
        Assert.Equal(Invitation.HashOf(first), Invitation.HashOf($" {first} "));
        Assert.NotEqual(first, Invitation.HashOf(first));
        Assert.Equal(64, Invitation.HashOf(first).Length);
    }

    [Fact]
    public void An_invitation_expires_at_its_expiry_moment()
    {
        var created = DateTimeOffset.Parse("2026-10-18T00:00:00Z");
        var invitation = new Invitation("h", "org", "a@b", "A", created, created + Invitation.Lifetime);

        Assert.False(invitation.IsExpired(created.AddDays(6)));
        Assert.True(invitation.IsExpired(created.AddDays(7)));
    }
}
