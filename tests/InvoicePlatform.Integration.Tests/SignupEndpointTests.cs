using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

/// <summary>Self sign-up through the API. Each test gets its own factory, because opening sign-up is platform-wide.</summary>
public class SignupEndpointTests
{
    private static object Request(string email) => new
    {
        name = "Anna Peeters",
        email,
        password = "lang-genoeg-wachtwoord",
        organizationName = "Bakkerij Peeters",
        countryCode = "BE",
    };

    private static async Task Open(SignedInFactory factory)
        => Assert.Equal(HttpStatusCode.OK,
            (await factory.As("platform-a").PutAsJsonAsync("/api/v1/platform/settings", new { extractionModel = (string?)null, signupOpen = true })).StatusCode);

    [Fact]
    public async Task Signup_is_closed_by_default()
    {
        using var factory = new SignedInFactory();
        var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/signup/status");
        var attempt = await client.PostAsJsonAsync("/api/v1/signup", Request("anna@bakkerij.be"));

        Assert.False(status.GetProperty("open").GetBoolean());
        Assert.Equal(1, status.GetProperty("trialConversions").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, attempt.StatusCode);
        Assert.Empty(factory.Mail.Sent);
    }

    [Fact]
    public async Task A_confirmed_signup_signs_in_an_admin_after_mfa_setup_whose_organisation_is_on_trial()
    {
        using var factory = new SignedInFactory();
        await Open(factory);
        var client = factory.CreateClient();

        var signup = await client.PostAsJsonAsync("/api/v1/signup", Request("anna@bakkerij.be"));
        var mail = Assert.Single(factory.Mail.Sent);
        var code = Uri.UnescapeDataString(mail.Body.Split("?code=")[1].Split('\n')[0].Trim());
        var confirm = await client.PostAsJsonAsync("/api/v1/signup/confirm", new { code });

        Assert.Equal(HttpStatusCode.Accepted, signup.StatusCode);
        Assert.Equal("anna@bakkerij.be", mail.To);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var body = await confirm.Content.ReadFromJsonAsync<JsonElement>();

        // The new user is an Admin, so MFA is set up before there is a session.
        Assert.True(body.GetProperty("mfaSetupRequired").GetBoolean());
        Assert.False(body.TryGetProperty("token", out _));
        var (token, _, _) = await MfaEndpointTests.FinishSetupAsync(client, body.GetProperty("challenge").GetString()!);

        var me = await factory.As(token).GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal("Admin", me.GetProperty("role").GetString());
        var organizationId = me.GetProperty("organizationId").GetString()!;
        Assert.Equal(new InvoicePlatform.Application.Usage.UsageSummary(0, 1, Trial: true), await factory.Usage.GetAsync(organizationId));
        var usage = await factory.As(token).GetFromJsonAsync<JsonElement>("/api/v1/organization/usage");
        Assert.Equal(1, usage.GetProperty("limit").GetInt32());
        Assert.False(usage.GetProperty("canExport").GetBoolean());
    }

    [Fact]
    public async Task A_wrong_code_is_refused()
    {
        using var factory = new SignedInFactory();

        var confirm = await factory.CreateClient().PostAsJsonAsync("/api/v1/signup/confirm", new { code = "not-a-real-code" });

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
    }

    [Fact]
    public async Task Existing_organisations_have_no_limit()
    {
        using var factory = new SignedInFactory();

        var usage = await factory.As("readonly-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/usage");

        Assert.Equal(JsonValueKind.Null, usage.GetProperty("limit").ValueKind);
        Assert.True(usage.GetProperty("canExport").GetBoolean());
    }
}
