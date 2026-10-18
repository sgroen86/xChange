using System.Net;
using System.Text;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Infrastructure.Anthropic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The API caches the Anthropic key for the life of a Lambda instance. When the
/// key is rotated in Secrets Manager - or expires, as it did on 2026-10-08 - a
/// warm instance must not keep failing with the old one, and a key that is
/// really invalid must say so instead of surfacing as a bare 500.
///
/// Anthropic is replaced by a handler that accepts one key and rejects others.
/// </summary>
public class AnthropicKeyRotationTests
{
    private const string ExpiredKey = "sk-ant-expired";
    private const string RotatedKey = "sk-ant-rotated";

    [Fact]
    public async Task A_rejected_key_is_fetched_again_and_the_request_retried()
    {
        var keys = new RotatingKeys(ExpiredKey, RotatedKey);
        var interpreter = Interpreter(keys, acceptedKey: RotatedKey);

        var result = await interpreter.InterpretAsync("%PDF-1.4"u8.ToArray());

        Assert.Equal("F-1", result.Draft.InvoiceNumber);
        Assert.Equal(1, keys.Invalidations);
    }

    [Fact]
    public async Task A_key_that_stays_rejected_is_reported_as_such()
    {
        var keys = new RotatingKeys(ExpiredKey, ExpiredKey);
        var interpreter = Interpreter(keys, acceptedKey: RotatedKey);

        var error = await Assert.ThrowsAsync<InvoiceInterpretationException>(
            () => interpreter.InterpretAsync("%PDF-1.4"u8.ToArray()));

        Assert.Contains("API key", error.Message, StringComparison.Ordinal);
        Assert.Contains("xchange/anthropic-api-key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_api_still_builds_its_interpreter_without_a_test_http_client()
    {
        using var factory = new SignedInFactory();

        var interpreter = factory.Services.GetRequiredService<IInvoiceInterpreter>();

        Assert.IsType<AnthropicInvoiceInterpreter>(interpreter);
    }

    private static AnthropicInvoiceInterpreter Interpreter(IAnthropicApiKeyProvider keys, string acceptedKey)
        => new(
            keys,
            Options.Create(new AnthropicOptions { Model = "claude-test" }),
            NullLogger<AnthropicInvoiceInterpreter>.Instance,
            new HttpClient(new FakeAnthropic(acceptedKey)));

    /// <summary>Hands out the first key until invalidated, then the second.</summary>
    private sealed class RotatingKeys(string first, string afterInvalidation) : IAnthropicApiKeyProvider
    {
        public int Invalidations { get; private set; }

        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Invalidations == 0 ? first : afterInvalidation);

        public void Invalidate() => Invalidations++;
    }

    private sealed class FakeAnthropic(string acceptedKey) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.Headers.TryGetValues("x-api-key", out var values) ? values.Single() : null;

            if (key != acceptedKey)
            {
                return Task.FromResult(Json(HttpStatusCode.Unauthorized,
                    """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}"""));
            }

            const string extracted = """{\"invoiceNumber\":\"F-1\"}""";
            return Task.FromResult(Json(HttpStatusCode.OK, $$$"""
                {"id":"msg_1","type":"message","role":"assistant","model":"claude-test",
                 "content":[{"type":"text","text":"{{{extracted}}}"}],
                 "stop_reason":"end_turn","stop_sequence":null,
                 "usage":{"input_tokens":1,"output_tokens":1}}
                """));
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
