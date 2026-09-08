using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

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
/// everyone out, and in the other direction leaves a paid endpoint open.
/// </summary>
public class RouteProtectionTests : IClassFixture<RouteProtectionTests.Factory>
{
    private readonly Factory _factory;

    public RouteProtectionTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Extraction_requires_a_login()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("%PDF-1.4"u8.ToArray()), "file", "invoice.pdf");

        var response = await client.PostAsync("/api/v1/invoices/extract", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Extraction_rejects_a_bogus_token_rather_than_accepting_it()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer not-a-real-session");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("%PDF-1.4"u8.ToArray()), "file", "invoice.pdf");

        var response = await client.PostAsync("/api/v1/invoices/extract", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/status")]
    public async Task Public_auth_routes_are_reachable_without_a_login(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        // The status code depends on whether a real DynamoDB table is reachable,
        // which it is not from a test host - so this asserts the one thing that
        // matters here: the route is not gated behind a login.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_stays_public()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The identity store builds an AWS client at resolution time, which needs a
    /// region even though these tests never reach the network.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "eu-central-1");
            Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", "eu-central-1");
            Environment.SetEnvironmentVariable("Anthropic__ApiKey", "test-key-not-used");
        }
    }
}
