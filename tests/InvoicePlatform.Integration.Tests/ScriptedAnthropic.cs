using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Stands in for Anthropic. The interpreter sends a header call and a lines
/// call at the same time; this tells them apart by the lines call's task text
/// and answers each with its own script. Every request body is kept.
/// </summary>
internal sealed class ScriptedAnthropic : HttpMessageHandler
{
    public const string LinesTask = "Extract only the invoice lines";

    private readonly ConcurrentQueue<string> _bodies = new();

    public Func<CancellationToken, Task<HttpResponseMessage>> Header { get; init; } = _ => Task.FromResult(Reply("{}"));

    public Func<CancellationToken, Task<HttpResponseMessage>> Lines { get; init; } = _ => Task.FromResult(Reply("""{"lines":[],"evidence":[]}"""));

    public IReadOnlyList<string> Bodies => [.. _bodies];

    public string HeaderBody => Bodies.Single(b => !b.Contains(LinesTask, StringComparison.Ordinal));

    public string LinesBody => Bodies.Single(b => b.Contains(LinesTask, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        _bodies.Enqueue(body);
        return await (body.Contains(LinesTask, StringComparison.Ordinal) ? Lines : Header)(cancellationToken);
    }

    /// <summary>A message whose only text block is <paramref name="json"/>.</summary>
    public static HttpResponseMessage Reply(string json, string stopReason = "end_turn", int inputTokens = 1, int outputTokens = 1)
    {
        var message = new
        {
            id = "msg_1",
            type = "message",
            role = "assistant",
            model = "claude-test",
            content = new[] { new { type = "text", text = json } },
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = inputTokens, output_tokens = outputTokens },
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage Error(HttpStatusCode status, string type) => new(status)
    {
        Content = new StringContent(
            $$$"""{"type":"error","error":{"type":"{{{type}}}","message":"scripted"}}""", Encoding.UTF8, "application/json"),
    };

    /// <summary>Never answers; ends only when the request is cancelled.</summary>
    public static async Task<HttpResponseMessage> Hang(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("unreachable");
    }
}
