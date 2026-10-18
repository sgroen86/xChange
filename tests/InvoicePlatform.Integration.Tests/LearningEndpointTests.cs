using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Infrastructure.Anthropic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Integration.Tests;

public class LearningEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public LearningEndpointTests(SignedInFactory factory) => _factory = factory;

    private async Task<string> Seed(string organizationId)
    {
        var id = Guid.NewGuid().ToString("n")[..16];
        var known = await _factory.Learning.GetCorrectionsAsync(organizationId);
        await _factory.Learning.SaveCorrectionsAsync(organizationId,
        [
            .. known,
            new LearnedCorrection(id, "organization", "seller.vatIdentifier", null, "NL857231409B01", null, CorrectionUse.Apply, 2, DateTimeOffset.UnixEpoch),
        ]);
        return id;
    }

    [Fact]
    public async Task The_organisations_corrections_are_listed_for_its_users_only()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var own = await _factory.As("readonly-a").GetFromJsonAsync<JsonElement>("/api/v1/learning");
        var other = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>("/api/v1/learning");

        var item = own.EnumerateArray().Single(e => e.GetProperty("id").GetString() == id);
        Assert.Equal("apply", item.GetProperty("use").GetString());
        Assert.Equal("NL857231409B01", item.GetProperty("to").GetString());
        Assert.DoesNotContain(other.EnumerateArray(), e => e.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task A_correction_is_forgotten_by_its_own_organisation_only()
    {
        var id = await Seed(SignedInFactory.OrgA);

        var byOther = await _factory.As("admin-b").DeleteAsync($"/api/v1/learning/{id}");
        var byReadOnly = await _factory.As("readonly-a").DeleteAsync($"/api/v1/learning/{id}");
        var byUser = await _factory.As("user-a").DeleteAsync($"/api/v1/learning/{id}");

        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byReadOnly.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byUser.StatusCode);
        Assert.DoesNotContain(await _factory.Learning.GetCorrectionsAsync(SignedInFactory.OrgA), c => c.Id == id);
    }

    [Fact]
    public async Task Hints_reach_the_model_as_a_separate_data_block()
    {
        var anthropic = new CapturingAnthropic();
        var interpreter = new AnthropicInvoiceInterpreter(
            new FixedKey(),
            Options.Create(new AnthropicOptions { Model = "claude-test" }),
            NullLogger<AnthropicInvoiceInterpreter>.Instance,
            new HttpClient(anthropic));

        await interpreter.InterpretAsync("%PDF-1.4"u8.ToArray(), [new ReadingHint("buyerReference", null, "PO-4502", null)]);

        var body = JsonDocument.Parse(anthropic.Body!).RootElement;
        var texts = body.GetProperty("messages")[0].GetProperty("content").EnumerateArray()
            .Where(b => b.GetProperty("type").GetString() == "text")
            .Select(b => b.GetProperty("text").GetString()!)
            .ToList();
        Assert.Contains(texts, t => t.StartsWith("reviewer_corrections", StringComparison.Ordinal) && t.Contains("PO-4502", StringComparison.Ordinal));
        Assert.Contains("reviewer_corrections", body.GetProperty("system").GetString(), StringComparison.Ordinal);
    }

    private sealed class FixedKey : IAnthropicApiKeyProvider
    {
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("sk-ant-test");

        public void Invalidate()
        {
        }
    }

    private sealed class CapturingAnthropic : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string extracted = """{\"invoiceNumber\":\"F-1\"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$$"""
                    {"id":"msg_1","type":"message","role":"assistant","model":"claude-test",
                     "content":[{"type":"text","text":"{{{extracted}}}"}],
                     "stop_reason":"end_turn","stop_sequence":null,
                     "usage":{"input_tokens":1,"output_tokens":1}}
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }
}
