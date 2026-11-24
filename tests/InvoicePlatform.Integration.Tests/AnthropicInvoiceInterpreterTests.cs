using System.Net;
using System.Text.Json;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Infrastructure.Anthropic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Integration.Tests;

public class AnthropicInvoiceInterpreterTests
{
    private static readonly byte[] Pdf = "%PDF-1.4 test"u8.ToArray();

    [Fact]
    public async Task Two_requests_go_out_with_their_own_schema_and_the_same_pdf()
    {
        var anthropic = new ScriptedAnthropic();

        await Interpreter(anthropic).InterpretAsync(Pdf);

        Assert.Equal(2, anthropic.Bodies.Count);
        var header = JsonDocument.Parse(anthropic.HeaderBody).RootElement;
        var lines = JsonDocument.Parse(anthropic.LinesBody).RootElement;
        Assert.Contains("seller", SchemaProperties(header));
        Assert.DoesNotContain("lines", SchemaProperties(header));
        Assert.Equal(["lines", "evidence"], SchemaProperties(lines));
        Assert.Equal(DocumentData(header), DocumentData(lines));
    }

    [Fact]
    public async Task The_answers_are_merged_into_one_draft()
    {
        var anthropic = new ScriptedAnthropic
        {
            Header = _ => Task.FromResult(ScriptedAnthropic.Reply("""
                {"invoiceNumber":"F-1","payment":{"bic":"INGBNL2A"},
                 "evidence":[{"field":"invoiceNumber","confidence":"0.9","pageNumber":"1","sourceText":"F-1"}]}
                """)),
            Lines = _ => Task.FromResult(ScriptedAnthropic.Reply("""
                {"lines":[{"description":"Schroeven","sellerItemIdentifier":"ART-7","quantity":"300","unitPrice":"12.50","baseQuantity":"100"}],
                 "evidence":[{"field":"lines[0].unitPrice","confidence":"0.8","pageNumber":"1","sourceText":"12,50 / 100 st"}]}
                """)),
        };

        var result = await Interpreter(anthropic).InterpretAsync(Pdf);

        Assert.Equal("F-1", result.Draft.InvoiceNumber);
        Assert.Equal("INGBNL2A", result.Draft.Payment!.Bic);
        var line = Assert.Single(result.Draft.Lines);
        Assert.Equal("ART-7", line.SellerItemIdentifier);
        Assert.Equal(100m, line.BaseQuantity);
        Assert.Equal(["invoiceNumber", "lines[0].unitPrice"], result.Draft.Evidence.Select(e => e.Field));
    }

    [Fact]
    public async Task The_tokens_of_both_calls_are_added_up()
    {
        var anthropic = new ScriptedAnthropic
        {
            Header = _ => Task.FromResult(ScriptedAnthropic.Reply("{}", inputTokens: 100, outputTokens: 10)),
            Lines = _ => Task.FromResult(ScriptedAnthropic.Reply("""{"lines":[]}""", inputTokens: 200, outputTokens: 20)),
        };

        var result = await Interpreter(anthropic).InterpretAsync(Pdf);

        Assert.Equal(300, result.Execution.Usage.InputTokens);
        Assert.Equal(30, result.Execution.Usage.OutputTokens);
        Assert.Equal("claude-test", result.Execution.Model);
    }

    [Fact]
    public async Task Hints_about_lines_go_to_the_lines_call_and_the_rest_to_the_header()
    {
        var anthropic = new ScriptedAnthropic();

        await Interpreter(anthropic).InterpretAsync(Pdf,
        [
            new ReadingHint("buyerReference", null, "PO-4502", null),
            new ReadingHint("lines.unitCode", "C62", "HUR", "Consultancy"),
        ]);

        Assert.Contains("PO-4502", UserTexts(anthropic.HeaderBody));
        Assert.DoesNotContain("HUR", UserTexts(anthropic.HeaderBody));
        Assert.Contains("HUR", UserTexts(anthropic.LinesBody));
        Assert.DoesNotContain("PO-4502", UserTexts(anthropic.LinesBody));
    }

    [Fact]
    public async Task A_call_without_hints_gets_no_corrections_block()
    {
        var anthropic = new ScriptedAnthropic();

        await Interpreter(anthropic).InterpretAsync(Pdf, [new ReadingHint("buyerReference", null, "PO-4502", null)]);

        Assert.DoesNotContain("reviewer_corrections (data", UserTexts(anthropic.LinesBody));
    }

    [Theory]
    [InlineData("max_tokens", "{\"invoiceNumber\":\"F-", InterpretationFailure.TooLarge)]
    [InlineData("refusal", "", InterpretationFailure.Declined)]
    [InlineData("end_turn", "not json", InterpretationFailure.Unreadable)]
    [InlineData("end_turn", "", InterpretationFailure.Unreadable)]
    public async Task A_bad_answer_has_its_reason(string stopReason, string text, InterpretationFailure expected)
    {
        var anthropic = new ScriptedAnthropic
        {
            Header = _ => Task.FromResult(ScriptedAnthropic.Reply(text, stopReason, inputTokens: 7, outputTokens: 3)),
        };

        var error = await Assert.ThrowsAsync<InvoiceInterpretationException>(() => Interpreter(anthropic).InterpretAsync(Pdf));

        Assert.Equal(expected, error.Reason);
        Assert.NotNull(error.Execution);
        Assert.True(error.Execution!.Usage.InputTokens >= 7);
    }

    [Fact]
    public async Task An_api_that_keeps_failing_is_unavailable_not_a_crash()
    {
        var anthropic = new ScriptedAnthropic
        {
            Header = _ => Task.FromResult(ScriptedAnthropic.Error(HttpStatusCode.InternalServerError, "api_error")),
        };

        var error = await Assert.ThrowsAsync<InvoiceInterpretationException>(() => Interpreter(anthropic).InterpretAsync(Pdf));

        Assert.Equal(InterpretationFailure.ProviderUnavailable, error.Reason);
    }

    [Fact]
    public async Task When_one_half_fails_the_other_is_stopped_and_the_whole_fails_with_what_was_paid()
    {
        var anthropic = new ScriptedAnthropic
        {
            Header = ScriptedAnthropic.Hang,
            Lines = _ => Task.FromResult(ScriptedAnthropic.Reply("{\"lines\":[{", "max_tokens", inputTokens: 50, outputTokens: 5)),
        };

        var error = await Assert.ThrowsAsync<InvoiceInterpretationException>(() => Interpreter(anthropic).InterpretAsync(Pdf));

        Assert.Equal(InterpretationFailure.TooLarge, error.Reason);
        Assert.Equal(50, error.Execution!.Usage.InputTokens);
    }

    [Fact]
    public async Task No_answer_within_the_time_limit_is_a_timeout()
    {
        var anthropic = new ScriptedAnthropic { Header = ScriptedAnthropic.Hang, Lines = ScriptedAnthropic.Hang };

        var error = await Assert.ThrowsAsync<InvoiceInterpretationException>(
            () => Interpreter(anthropic, timeoutSeconds: 1).InterpretAsync(Pdf));

        Assert.Equal(InterpretationFailure.TimedOut, error.Reason);
        Assert.Null(error.Execution);
    }

    [Fact]
    public async Task A_caller_that_goes_away_is_a_cancellation_not_a_failure()
    {
        var anthropic = new ScriptedAnthropic { Header = ScriptedAnthropic.Hang, Lines = ScriptedAnthropic.Hang };
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Interpreter(anthropic).InterpretAsync(Pdf, cancellationToken: caller.Token));
    }

    private static AnthropicInvoiceInterpreter Interpreter(ScriptedAnthropic anthropic, int timeoutSeconds = 30)
        => new(
            new FixedKey(),
            Options.Create(new AnthropicOptions { Model = "claude-test", TimeoutSeconds = timeoutSeconds }),
            NullLogger<AnthropicInvoiceInterpreter>.Instance,
            new HttpClient(anthropic));

    private static List<string> SchemaProperties(JsonElement body)
        => body.GetProperty("output_config").GetProperty("format").GetProperty("schema").GetProperty("properties")
            .EnumerateObject().Select(p => p.Name).ToList();

    private static string DocumentData(JsonElement body)
        => body.GetProperty("messages")[0].GetProperty("content").EnumerateArray()
            .Single(b => b.GetProperty("type").GetString() == "document")
            .GetProperty("source").GetProperty("data").GetString()!;

    private static string UserTexts(string body)
        => string.Join("\n", JsonDocument.Parse(body).RootElement.GetProperty("messages")[0].GetProperty("content").EnumerateArray()
            .Where(b => b.GetProperty("type").GetString() == "text")
            .Select(b => b.GetProperty("text").GetString()));

    private sealed class FixedKey : IAnthropicApiKeyProvider
    {
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("sk-ant-test");

        public void Invalidate()
        {
        }
    }
}
