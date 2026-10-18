using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

public class OrganizationEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public OrganizationEndpointTests(SignedInFactory factory) => _factory = factory;

    [Fact]
    public async Task Settings_list_the_available_rulesets()
    {
        var body = await _factory.As("readonly-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/settings");

        var first = body.GetProperty("rulesets").EnumerateArray().First();
        Assert.Equal("peppol-bis-billing-3.0", first.GetProperty("id").GetString());
        Assert.Equal("3.0.21", first.GetProperty("rulesRelease").GetString());
    }

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Only_an_admin_may_change_settings(string token)
    {
        var response = await _factory.As(token).PutAsJsonAsync("/api/v1/organization/settings", new { endpointId = "12345678", endpointScheme = "0106" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admins_change_round_trips_and_stays_in_the_organisation()
    {
        var put = await _factory.As("admin-b").PutAsJsonAsync("/api/v1/organization/settings",
            new { defaultRulesetId = "peppol-bis-billing-3.0", endpointId = "0123456749", endpointScheme = "0208" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var mine = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>("/api/v1/organization/settings");
        Assert.Equal("0123456749", mine.GetProperty("endpointId").GetString());

        var theirs = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/settings");
        Assert.NotEqual("0123456749", theirs.GetProperty("endpointId").GetString());
    }

    [Fact]
    public async Task An_unknown_ruleset_is_refused()
    {
        var response = await _factory.As("admin-a").PutAsJsonAsync("/api/v1/organization/settings", new { defaultRulesetId = "xrechnung-9" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
