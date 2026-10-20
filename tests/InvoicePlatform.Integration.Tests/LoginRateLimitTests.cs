using System.Net;
using System.Text.Json;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The lockout through the whole pipeline, forwarded-headers middleware
/// included, and what a session looks like in the identity table.
/// </summary>
public class LoginRateLimitTests
{
    private const string Password = "a-long-enough-password";

    /// <summary>Test-only header carrying the address the connection should appear to come from.</summary>
    private const string SourceIpHeader = "X-Test-Source-Ip";

    /// <summary>
    /// A login as a Lambda Function URL delivers it: the connection's address
    /// is the one AWS saw, and X-Forwarded-For holds whatever the caller put
    /// there, with the real address appended.
    /// </summary>
    private static async Task<int> LoginAsync(WebApplicationFactory<Program> factory, string sourceIp, string forgedForwardedFor, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { email = "piet@a", password }), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", $"{forgedForwardedFor}, {sourceIp}");
        request.Headers.TryAddWithoutValidation(SourceIpHeader, sourceIp);
        return (int)(await factory.CreateClient().SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task A_forged_forwarded_for_header_does_not_give_fresh_attempts()
    {
        var attempts = new InMemoryAttempts();
        using var root = new SignedInFactory();
        await root.AddUserAsync("piet@a", Password, UserRole.User);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoginAttemptStore>();
            services.AddSingleton<ILoginAttemptStore>(attempts);
            services.AddSingleton<IStartupFilter, SourceIpFilter>();
        }));

        for (var i = 0; i < LoginAttempts.MaxAttempts; i++)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, await LoginAsync(factory, "198.51.100.7", $"10.0.0.{i}", "wrong-password-here"));
        }

        // A new made-up address in the header changes nothing: still locked.
        Assert.Equal(StatusCodes.Status429TooManyRequests, await LoginAsync(factory, "198.51.100.7", "10.0.0.99", Password));
        Assert.Contains("198.51.100.7", attempts.Keys);
        Assert.DoesNotContain(attempts.Keys, key => key.StartsWith("10.0.0.", StringComparison.Ordinal));

        // Whereas the lock is on the real address: elsewhere the right password works.
        Assert.Equal(StatusCodes.Status200OK, await LoginAsync(factory, "203.0.113.20", "198.51.100.7", Password));
    }

    [Fact]
    public void A_stored_session_holds_the_hash_of_its_token_and_never_the_token()
    {
        var token = AuthSession.NewToken();
        var now = DateTimeOffset.UtcNow;

        var item = DynamoDbIdentityStore.SessionItem(
            new AuthSession(AuthSession.HashOf(token), "user-1", "org-1", now, now + AuthSession.Lifetime));

        Assert.Equal($"SESSION#{AuthSession.HashOf(token)}", item["pk"].S);
        Assert.All(item.Values, value =>
            Assert.DoesNotContain(token, value.S ?? value.N ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_session_from_a_login_works_until_logout()
    {
        using var factory = new SignedInFactory();
        await factory.AddUserAsync("piet@a", Password, UserRole.User);

        var login = await factory.CreateClient().PostAsync(
            "/api/v1/auth/login",
            new StringContent(JsonSerializer.Serialize(new { email = "piet@a", password = Password }), System.Text.Encoding.UTF8, "application/json"));
        var token = (await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync())).RootElement.GetProperty("token").GetString()!;
        var client = factory.As(token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    /// <summary>
    /// Plays the Lambda host: sets the connection's address before anything
    /// of the application runs, forwarded-headers middleware included - as the
    /// host fills it from requestContext.http.sourceIp.
    /// </summary>
    private sealed class SourceIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[SourceIpHeader].ToString(), out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }

    /// <summary>A working lockout store; the factory's own never locks, so other tests can log in freely.</summary>
    private sealed class InMemoryAttempts : ILoginAttemptStore
    {
        private readonly Dictionary<string, LoginAttempts> _attempts = [];

        public IReadOnlyList<string> Keys
        {
            get
            {
                lock (_attempts)
                {
                    return _attempts.Keys.ToList();
                }
            }
        }

        public Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
        {
            lock (_attempts)
            {
                return Task.FromResult(_attempts.GetValueOrDefault(clientKey));
            }
        }

        public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default)
        {
            lock (_attempts)
            {
                _attempts[attempts.ClientKey] = attempts;
            }

            return Task.CompletedTask;
        }

        public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default)
        {
            lock (_attempts)
            {
                _attempts.Remove(clientKey);
            }

            return Task.CompletedTask;
        }
    }
}
