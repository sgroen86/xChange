using System.Net;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Guards which routes need a login.
///
/// This exists because of a real defect: the authorisation filters were chained
/// onto the result of MapInvoiceEndpoints, which returns the whole /api/v1
/// group rather than the endpoint. Every route in the group was protected,
/// including login - so the login page could never load, and the only symptom
/// was a 401 from the endpoint that decides whether to show it.
///
/// The asymmetry is the point: getting this wrong in one direction locks
/// everyone out, and in the other leaves a paid endpoint open.
/// </summary>
public class RouteProtectionTests : IClassFixture<RouteProtectionTests.Factory>
{
    private const string TestOrigin = "https://greenitsolutions.net";

    private readonly Factory _factory;

    public RouteProtectionTests(Factory factory) => _factory = factory;

    private static MultipartFormDataContent APdf()
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("%PDF-1.4"u8.ToArray()), "file", "invoice.pdf");
        return content;
    }

    [Fact]
    public async Task Extraction_requires_a_login()
    {
        var client = _factory.CreateClient();

        using var content = APdf();
        var response = await client.PostAsync("/api/v1/invoices/extract", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Extraction_rejects_a_bogus_token_rather_than_accepting_it()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer not-a-real-session");

        using var content = APdf();
        var response = await client.PostAsync("/api/v1/invoices/extract", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_auth_routes_are_reachable_without_a_login()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("setupRequired", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_stays_public()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Exactly_one_cors_origin_header_is_returned()
    {
        // Two Access-Control-Allow-Origin headers are rejected by browsers but
        // look perfectly fine to curl and to a status-code assertion, so the
        // count is asserted explicitly. Applying the policy both globally and
        // per-endpoint produced exactly that, and it reached production.
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/status");
        request.Headers.Add("Origin", TestOrigin);

        var response = await client.SendAsync(request);

        var origins = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? values.ToArray()
            : [];

        Assert.Single(origins);
    }

    /// <summary>
    /// Substitutes in-memory stores for DynamoDB.
    ///
    /// Without this the tests pass or fail depending on whether the machine
    /// happens to have AWS credentials - green on a developer laptop, 500s on a
    /// CI runner. A test whose result depends on ambient credentials is not
    /// testing what it claims to.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        internal const string TestOrigin = RouteProtectionTests.TestOrigin;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("Anthropic:ApiKey", "test-key-never-used");

            // The test host runs as Development, whose configured origins are
            // localhost. Set the origin the CORS test asserts on so the test
            // does not depend on which appsettings file happens to load.
            builder.UseSetting("Cors:AllowedOrigins:0", TestOrigin);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserStore>();
                services.RemoveAll<ISessionStore>();
                services.RemoveAll<ILoginAttemptStore>();

                var store = new EmptyIdentityStore();
                services.AddSingleton<IUserStore>(store);
                services.AddSingleton<ISessionStore>(store);
                services.AddSingleton<ILoginAttemptStore>(store);
            });
        }
    }

    /// <summary>An identity store with nobody in it: no users, no sessions.</summary>
    private sealed class EmptyIdentityStore : IUserStore, ISessionStore, ILoginAttemptStore
    {
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<User?> FindByIdAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateLastLoginAsync(string organizationId, string userId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<AuthSession?> FindAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult<AuthSession?>(null);

        public Task DeleteAsync(string token, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginAttempts?>(null);

        public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
