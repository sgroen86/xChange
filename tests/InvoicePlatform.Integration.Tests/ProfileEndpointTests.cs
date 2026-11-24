using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

public class ProfileEndpointTests : IClassFixture<SignedInFactory>
{
    private const string Route = "/api/v1/organization/profile";

    private readonly SignedInFactory _factory;

    public ProfileEndpointTests(SignedInFactory factory) => _factory = factory;

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Everyone_in_the_organisation_can_read_the_profile(string token)
    {
        var response = await _factory.As(token).GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Only_an_admin_may_change_the_profile(string token)
    {
        var response = await _factory.As(token).PutAsJsonAsync(Route, new { general = new { legalName = "Alfa B.V." } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admins_profile_round_trips_and_stays_in_the_organisation()
    {
        var put = await _factory.As("admin-b").PutAsJsonAsync(Route, new
        {
            general = new { legalName = "Beta B.V.", vatIdentifier = "nl123456789b01", iban = "NL91 ABNA 0417 1643 00" },
            peppol = new { endpointId = "12345678", endpointScheme = "0106" },
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var mine = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>(Route);
        Assert.Equal("NL123456789B01", mine.GetProperty("general").GetProperty("vatIdentifier").GetString());
        Assert.Equal("NL91ABNA0417164300", mine.GetProperty("general").GetProperty("iban").GetString());
        Assert.Equal("12345678", mine.GetProperty("peppol").GetProperty("endpointId").GetString());

        var theirs = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>(Route);
        Assert.NotEqual("Beta B.V.", theirs.GetProperty("general").GetProperty("legalName").GetString());
    }

    [Fact]
    public async Task An_invalid_vat_number_is_refused_with_an_error_on_that_field()
    {
        var response = await _factory.As("admin-a").PutAsJsonAsync(Route, new { general = new { vatIdentifier = "NL123" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("general.vatIdentifier", out _));
    }
}
