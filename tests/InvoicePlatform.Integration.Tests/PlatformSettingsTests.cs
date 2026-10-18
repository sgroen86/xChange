using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

public class PlatformSettingsTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public PlatformSettingsTests(SignedInFactory factory) => _factory = factory;

    [Fact]
    public async Task Only_the_platform_administrator_chooses_the_model()
    {
        var byOrgAdmin = await _factory.As("admin-a").PutAsJsonAsync("/api/v1/platform/settings", new { extractionModel = "claude-opus-5-5" });
        var readByOrgAdmin = await _factory.As("admin-a").GetAsync("/api/v1/platform/settings");

        Assert.Equal(HttpStatusCode.Forbidden, byOrgAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readByOrgAdmin.StatusCode);
    }

    [Fact]
    public async Task The_model_is_one_of_the_offered_ones()
    {
        var client = _factory.As("platform-a");

        var unknown = await client.PutAsJsonAsync("/api/v1/platform/settings", new { extractionModel = "gpt-9" });
        var chosen = await client.PutAsJsonAsync("/api/v1/platform/settings", new { extractionModel = "claude-opus-5-5" });
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/settings");

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Equal("claude-opus-5-5", settings.GetProperty("extractionModel").GetString());
        Assert.Contains(settings.GetProperty("models").EnumerateArray(), m => m.GetProperty("id").GetString() == "claude-opus-5");

        // Back to the configured default.
        await client.PutAsJsonAsync("/api/v1/platform/settings", new { extractionModel = (string?)null });
        Assert.Equal(JsonValueKind.Null, (await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/settings")).GetProperty("extractionModel").ValueKind);
    }
}
