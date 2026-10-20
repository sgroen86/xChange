using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Two-step verification through the API, with the real clock and an in-memory
/// store. Each test that changes someone's MFA gets its own factory.
/// </summary>
public class MfaEndpointTests
{
    private const string Password = "a-long-enough-password";

    /// <summary>What the phone shows now, or <paramref name="ahead"/> steps later (to avoid replaying a used step).</summary>
    internal static string CodeFor(string secret, int ahead = 0)
        => Totp.CodeAt(Base32.Decode(secret), Totp.StepAt(DateTimeOffset.UtcNow) + ahead);

    /// <summary>The setup a login requires, as the web app does it.</summary>
    internal static async Task<(string Token, string Secret, string[] RecoveryCodes)> FinishSetupAsync(HttpClient client, string challenge)
    {
        var setup = await client.PostAsJsonAsync("/api/v1/auth/mfa/setup", new { challenge });
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var secret = (await setup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;

        var confirm = await client.PostAsJsonAsync("/api/v1/auth/mfa/setup/confirm", new { challenge, code = CodeFor(secret) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var session = await confirm.Content.ReadFromJsonAsync<JsonElement>();
        return (
            session.GetProperty("token").GetString()!,
            secret,
            session.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray());
    }

    private static async Task<JsonElement> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task An_admin_sets_up_mfa_at_login_and_from_then_on_needs_a_code()
    {
        using var factory = new SignedInFactory();
        await factory.AddUserAsync("boss@a", Password, UserRole.Admin);
        var client = factory.CreateClient();

        var first = await LoginAsync(client, "boss@a");
        Assert.True(first.GetProperty("mfaSetupRequired").GetBoolean());
        Assert.False(first.GetProperty("mfaRequired").GetBoolean());
        Assert.False(first.TryGetProperty("token", out _));
        Assert.False(first.TryGetProperty("user", out _));

        var (token, secret, recoveryCodes) = await FinishSetupAsync(client, first.GetProperty("challenge").GetString()!);
        Assert.Equal(10, recoveryCodes.Length);
        Assert.Equal(HttpStatusCode.OK, (await factory.As(token).GetAsync("/api/v1/auth/me")).StatusCode);

        var second = await LoginAsync(client, "boss@a");
        Assert.True(second.GetProperty("mfaRequired").GetBoolean());
        var challenge = second.GetProperty("challenge").GetString()!;

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login/mfa", new { challenge, code = "000000" });
        var right = await client.PostAsJsonAsync("/api/v1/auth/login/mfa", new { challenge, code = CodeFor(secret, ahead: 1) });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.False(string.IsNullOrEmpty((await right.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()));

        var third = await LoginAsync(client, "boss@a");
        var withRecovery = await client.PostAsJsonAsync(
            "/api/v1/auth/login/mfa", new { challenge = third.GetProperty("challenge").GetString(), recoveryCode = recoveryCodes[0] });
        var recovered = await withRecovery.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, withRecovery.StatusCode);
        Assert.Equal(9, recovered.GetProperty("recoveryCodesLeft").GetInt32());
    }

    [Fact]
    public async Task A_used_or_unknown_challenge_means_logging_in_again()
    {
        using var factory = new SignedInFactory();
        await factory.AddUserAsync("boss@a", Password, UserRole.Admin);
        var client = factory.CreateClient();
        var challenge = (await LoginAsync(client, "boss@a")).GetProperty("challenge").GetString()!;
        await FinishSetupAsync(client, challenge);

        var reused = await client.PostAsJsonAsync("/api/v1/auth/mfa/setup", new { challenge });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login/mfa", new { challenge = new string('f', 64), code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task The_wrong_code_message_follows_the_callers_language()
    {
        using var factory = new SignedInFactory();
        await factory.AddUserAsync("boss@a", Password, UserRole.Admin);
        var client = factory.CreateClient();
        var challenge = (await LoginAsync(client, "boss@a")).GetProperty("challenge").GetString()!;
        await client.PostAsJsonAsync("/api/v1/auth/mfa/setup", new { challenge });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/setup/confirm")
        {
            Content = JsonContent.Create(new { challenge, code = "000000" }),
        };
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
        var problem = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Invalid code.", problem.GetProperty("detail").GetString());
        Assert.Equal("Verification failed", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_ordinary_user_still_logs_in_with_the_password_alone()
    {
        using var factory = new SignedInFactory();
        await factory.AddUserAsync("piet@a", Password, UserRole.User);

        var session = await LoginAsync(factory.CreateClient(), "piet@a");

        Assert.False(string.IsNullOrEmpty(session.GetProperty("token").GetString()));
        Assert.Equal("User", session.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task An_ordinary_user_turns_mfa_on_renews_the_codes_and_turns_it_off()
    {
        using var factory = new SignedInFactory();
        var client = factory.As("user-a");

        var before = await client.GetFromJsonAsync<JsonElement>("/api/v1/account/mfa");
        Assert.False(before.GetProperty("enabled").GetBoolean());
        Assert.False(before.GetProperty("mandatory").GetBoolean());

        var setup = await (await client.PostAsync("/api/v1/account/mfa/setup", null)).Content.ReadFromJsonAsync<JsonElement>();
        var secret = setup.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/xChange:user%40a?secret=" + secret, setup.GetProperty("otpauthUri").GetString());

        var confirm = await client.PostAsJsonAsync("/api/v1/account/mfa/confirm", new { code = CodeFor(secret) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(10, (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes").GetArrayLength());
        Assert.NotNull(factory.Mfa.Find(SignedInFactory.OrgA, "user-a")!.ProtectedSecret);

        var renewed = await client.PostAsJsonAsync("/api/v1/account/mfa/recovery-codes", new { code = CodeFor(secret, ahead: 1) });
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);

        var on = await client.GetFromJsonAsync<JsonElement>("/api/v1/account/mfa");
        Assert.True(on.GetProperty("enabled").GetBoolean());
        Assert.Equal(10, on.GetProperty("recoveryCodesLeft").GetInt32());

        var wrong = await client.PostAsJsonAsync("/api/v1/account/mfa/disable", new { code = "000000" });
        var off = await client.PostAsJsonAsync(
            "/api/v1/account/mfa/disable",
            new { code = (await renewed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes")[0].GetString() });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/v1/account/mfa")).GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task An_admin_cannot_turn_mfa_off()
    {
        using var factory = new SignedInFactory();
        var client = factory.As("admin-a");
        var secret = (await (await client.PostAsync("/api/v1/account/mfa/setup", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("secret").GetString()!;
        await client.PostAsJsonAsync("/api/v1/account/mfa/confirm", new { code = CodeFor(secret) });

        var disable = await client.PostAsJsonAsync("/api/v1/account/mfa/disable", new { code = CodeFor(secret, ahead: 1) });
        var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/account/mfa");

        Assert.Equal(HttpStatusCode.BadRequest, disable.StatusCode);
        Assert.True(status.GetProperty("mandatory").GetBoolean());
        Assert.True(status.GetProperty("enabled").GetBoolean());
    }

    [Theory]
    [InlineData("GET", "/api/v1/account/mfa")]
    [InlineData("POST", "/api/v1/account/mfa/setup")]
    [InlineData("POST", "/api/v1/account/mfa/confirm")]
    [InlineData("POST", "/api/v1/account/mfa/disable")]
    [InlineData("POST", "/api/v1/account/mfa/recovery-codes")]
    public async Task Account_routes_need_a_session(string method, string path)
    {
        using var factory = new SignedInFactory();
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Only_the_platform_administrator_resets_someones_mfa()
    {
        using var factory = new SignedInFactory();
        var admin = factory.As("admin-a");
        var secret = (await (await admin.PostAsync("/api/v1/account/mfa/setup", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("secret").GetString()!;
        await admin.PostAsJsonAsync("/api/v1/account/mfa/confirm", new { code = CodeFor(secret) });

        HttpContent Body(string email) => new StringContent(JsonSerializer.Serialize(new { email }), Encoding.UTF8, "application/json");
        const string Path = "/api/v1/platform/users/mfa/reset";

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync(Path, Body("admin@a"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync(Path, Body("admin@a"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.As("platform-a").PostAsync(Path, Body("nobody@a"))).StatusCode);
        Assert.NotNull(factory.Mfa.Find(SignedInFactory.OrgA, "admin-a"));

        Assert.Equal(HttpStatusCode.NoContent, (await factory.As("platform-a").PostAsync(Path, Body("ADMIN@a"))).StatusCode);
        Assert.Null(factory.Mfa.Find(SignedInFactory.OrgA, "admin-a"));
    }
}
