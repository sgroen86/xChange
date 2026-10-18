using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Integration.Tests;

public class RelationEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public RelationEndpointTests(SignedInFactory factory) => _factory = factory;

    private async Task<string> Seed(string organizationId)
    {
        var id = Guid.NewGuid().ToString("n");
        await _factory.Relations.SaveAsync(new Relation
        {
            RelationId = id,
            OrganizationId = organizationId,
            Name = "Van Dijk Techniek B.V.",
            VatIdentifier = "NL814912345B01",
            UpdatedAt = DateTimeOffset.UnixEpoch,
        });
        return id;
    }

    private static object Update(string name) => new { name, vatIdentifier = "NL814912345B01", electronicAddress = "87654321", electronicAddressScheme = "0106", countryCode = "NL" };

    [Fact]
    public async Task Relations_require_a_login()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/relations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/relations/x", Update("x"))).StatusCode);
    }

    [Fact]
    public async Task A_user_corrects_a_relation_of_their_own_organisation()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var response = await _factory.As("user-a").PutAsJsonAsync($"/api/v1/relations/{id}", Update("Van Dijk"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await _factory.As("readonly-a").GetFromJsonAsync<JsonElement>("/api/v1/relations");
        var relation = list.EnumerateArray().Single(r => r.GetProperty("relationId").GetString() == id);
        Assert.Equal("87654321", relation.GetProperty("electronicAddress").GetString());
    }

    [Fact]
    public async Task A_read_only_account_cannot_correct()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var response = await _factory.As("readonly-a").PutAsJsonAsync($"/api/v1/relations/{id}", Update("X"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Another_organisations_relation_is_not_found_and_not_listed()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var put = await _factory.As("admin-b").PutAsJsonAsync($"/api/v1/relations/{id}", Update("Overgenomen"));
        var list = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>("/api/v1/relations");

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.DoesNotContain(list.EnumerateArray(), r => r.GetProperty("relationId").GetString() == id);
    }

    [Fact]
    public async Task A_customer_gets_its_own_export_format()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var response = await _factory.As("user-a").PutAsJsonAsync($"/api/v1/relations/{id}",
            new { name = "Brit Ltd", vatIdentifier = "GB123456789", countryCode = "GB", defaultRulesetId = "peppol-bis-billing-3.0" });
        var unknown = await _factory.As("user-a").PutAsJsonAsync($"/api/v1/relations/{id}",
            new { name = "Brit Ltd", vatIdentifier = "GB123456789", defaultRulesetId = "xrechnung-9" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("peppol-bis-billing-3.0",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("defaultRulesetId").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task An_invalid_correction_is_a_bad_request()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var response = await _factory.As("admin-a").PutAsJsonAsync($"/api/v1/relations/{id}",
            new { name = "X", vatIdentifier = "", legalRegistrationId = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
