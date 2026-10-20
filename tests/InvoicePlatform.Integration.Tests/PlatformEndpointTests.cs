using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Platform routes are for the platform administrator only - not for the Admin
/// of an organisation - and being platform administrator opens no door into
/// another organisation's data.
/// </summary>
public class PlatformEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public PlatformEndpointTests(SignedInFactory factory) => _factory = factory;

    private static object NewOrganization(string email) => new
    {
        name = "Klant Pte Ltd",
        countryCode = "SG",
        firstUser = new { name = "Jan", email },
    };

    private async Task<string> CreateAndGetCodeAsync(string email)
    {
        var response = await _factory.As("platform-a").PostAsJsonAsync("/api/v1/platform/organizations", NewOrganization(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("invitationCode").GetString()!;
    }

    [Theory]
    [InlineData("GET", "/api/v1/platform/organizations")]
    [InlineData("POST", "/api/v1/platform/organizations")]
    [InlineData("PUT", "/api/v1/platform/organizations/x")]
    [InlineData("POST", "/api/v1/platform/organizations/x/invitation")]
    public async Task Platform_routes_refuse_anonymous_and_organisation_admins(string method, string path)
    {
        HttpRequestMessage Request() => new(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().SendAsync(Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.As("admin-a").SendAsync(Request())).StatusCode);
    }

    [Fact]
    public async Task The_platform_administrator_creates_an_organisation_and_sees_it_listed()
    {
        await CreateAndGetCodeAsync("listed@klant.sg");

        var list = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>("/api/v1/platform/organizations");

        var created = list.EnumerateArray().First(o => o.GetProperty("pendingInvitationEmail").GetString() == "listed@klant.sg");
        Assert.Equal("SG", created.GetProperty("countryCode").GetString());
        Assert.True(created.GetProperty("invitationPending").GetBoolean());
    }

    [Fact]
    public async Task An_invitation_is_shown_publicly_and_accepted_once()
    {
        var code = await CreateAndGetCodeAsync("jan@klant.sg");
        var anonymous = _factory.CreateClient();

        var shown = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/invitations/{code}");
        Assert.Equal("jan@klant.sg", shown.GetProperty("email").GetString());
        Assert.Equal("Klant Pte Ltd", shown.GetProperty("organizationName").GetString());

        var accepted = await anonymous.PostAsJsonAsync($"/api/v1/invitations/{code}/accept", new { name = "Jan Tan", password = "a-long-enough-password" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        // An Admin: MFA is set up before the first session.
        var pending = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(pending.GetProperty("mfaSetupRequired").GetBoolean());
        var (token, _, _) = await MfaEndpointTests.FinishSetupAsync(anonymous, pending.GetProperty("challenge").GetString()!);
        var me = await _factory.As(token).GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal("Admin", me.GetProperty("role").GetString());
        Assert.Equal("jan@klant.sg", me.GetProperty("email").GetString());

        var again = await anonymous.PostAsJsonAsync($"/api/v1/invitations/{code}/accept", new { name = "Jan", password = "a-long-enough-password" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task An_unknown_invitation_is_not_found()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/invitations/{new string('0', 64)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Countries_are_public_and_use_iso_codes()
    {
        var countries = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/countries");

        var codes = countries.EnumerateArray().Select(c => c.GetProperty("code").GetString()).ToList();
        Assert.Equal(["SG", "GB", "NL", "BE"], codes);
    }

    [Fact]
    public async Task The_session_says_who_is_platform_administrator()
    {
        var platform = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var admin = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        Assert.True(platform.GetProperty("isPlatformAdmin").GetBoolean());
        Assert.False(admin.GetProperty("isPlatformAdmin").GetBoolean());
    }

    [Fact]
    public async Task Being_platform_administrator_opens_no_door_into_another_organisation()
    {
        var invoiceInB = await _factory.SeedInvoiceAsync(SignedInFactory.OrgB, TestInvoices.Complete());

        var response = await _factory.As("platform-a").GetAsync($"/api/v1/invoices/{invoiceInB}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
