using System.Net;
using System.Text;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The stored-invoice routes must be behind the login. An unauthenticated
/// listing would expose every tenant's invoices at once, so this is asserted
/// rather than assumed.
/// </summary>
public class StoredInvoiceEndpointTests : IClassFixture<RouteProtectionTests.Factory>
{
    private readonly RouteProtectionTests.Factory _factory;

    public StoredInvoiceEndpointTests(RouteProtectionTests.Factory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/v1/invoices")]
    [InlineData("/api/v1/invoices/some-id")]
    [InlineData("/api/v1/invoices/some-id/document")]
    public async Task Reading_invoices_requires_a_login(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Booking_requires_a_login()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/invoices",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_bogus_token_does_not_get_past_the_guard()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer not-a-real-session");

        var response = await client.GetAsync("/api/v1/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
