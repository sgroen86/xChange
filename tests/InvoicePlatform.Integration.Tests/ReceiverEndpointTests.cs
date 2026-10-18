using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoicePlatform.Application.Networks;
using InvoicePlatform.Domain.Networks;

namespace InvoicePlatform.Integration.Tests;

public class ReceiverEndpointTests : IClassFixture<SignedInFactory>
{
    private const string Nlcius = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:cen.eu:en16931:2017#compliant#urn:fdc:nen.nl:nlcius:v1.0::2.1";

    private readonly SignedInFactory _factory;

    public ReceiverEndpointTests(SignedInFactory factory) => _factory = factory;

    [Fact]
    public async Task Looking_up_a_receiver_requires_a_login()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/receivers/lookup", new { vatIdentifier = "NL814912345B01" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_registered_buyer_comes_back_with_its_address_and_formats()
    {
        _factory.Network.Answers["0106:11223344"] = NetworkAnswer.Registered([new DocumentTypeId("busdox-docid-qns", Nlcius)]);

        var response = await _factory.As("readonly-a").PostAsJsonAsync("/api/v1/receivers/lookup",
            new { vatIdentifier = "NL000000000B00", legalRegistrationId = "1122 3344", countryCode = "NL" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("found", body.GetProperty("status").GetString());
        Assert.Equal("0106", body.GetProperty("participant").GetProperty("scheme").GetString());
        Assert.Equal("11223344", body.GetProperty("participant").GetProperty("value").GetString());
        Assert.Equal(["nlcius-si-ubl-2.0"], body.GetProperty("acceptedRulesetIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["9944:NL000000000B00", "0106:11223344"], body.GetProperty("tried").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task A_buyer_without_numbers_is_not_looked_up()
    {
        var response = await _factory.As("user-a").PostAsJsonAsync("/api/v1/receivers/lookup", new { countryCode = "NL" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("nothingToLookUp", body.GetProperty("status").GetString());
    }
}
