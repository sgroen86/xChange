// Compares Claude models on real invoices, with the production extraction code
// (the same prompt, schema and parsing as AnthropicInvoiceInterpreter).
//
//   dotnet run --project tools/model-comparison -- <folder with PDFs> [model ...]
//
// Needs ANTHROPIC_API_KEY. Writes a Markdown report next to the PDFs
// (model-comparison.md) and prints a summary. The first model is the reference
// the others are compared with. Every call costs money; see the prices below.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Infrastructure.Anthropic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Dollars per million tokens (input, output), from public price lists on
// 2026-10-18 - check Anthropic's pricing page before deciding on them.
var prices = new Dictionary<string, (decimal Input, decimal Output)>
{
    ["claude-opus-5"] = (5m, 25m),
    ["claude-opus-5-5"] = (4m, 20m),
    ["claude-sonnet-5-5"] = (2m, 10m),
    ["claude-haiku-5-5"] = (0.10m, 0.50m),
};

if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("usage: model-comparison <folder with PDFs> [model ...]");
    return 2;
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
{
    Console.Error.WriteLine("Set ANTHROPIC_API_KEY first.");
    return 2;
}

var folder = args[0];
var models = args.Length > 1 ? args[1..] : [.. prices.Keys];
var pdfs = Directory.GetFiles(folder, "*.pdf").Order(StringComparer.OrdinalIgnoreCase).ToList();
if (pdfs.Count == 0)
{
    Console.Error.WriteLine($"No PDFs in {folder}.");
    return 2;
}

// The fields a reviewer would otherwise have to correct.
(string Name, Func<CanonicalInvoiceDraft, string?> Read)[] fields =
[
    ("Invoice number", d => d.InvoiceNumber),
    ("Type", d => d.TypeCode),
    ("Issue date", d => d.IssueDate),
    ("Due date", d => d.DueDate),
    ("Currency", d => d.CurrencyCode),
    ("Buyer reference", d => d.BuyerReference),
    ("Order reference", d => d.PurchaseOrderReference),
    ("Seller", d => d.Seller?.Name),
    ("Seller VAT", d => d.Seller?.VatIdentifier),
    ("Seller registration", d => d.Seller?.LegalRegistrationId),
    ("Buyer", d => d.Buyer?.Name),
    ("Buyer VAT", d => d.Buyer?.VatIdentifier),
    ("Buyer registration", d => d.Buyer?.LegalRegistrationId),
    ("IBAN", d => d.Payment?.Iban),
    ("Lines", d => d.Lines.Count.ToString(CultureInfo.InvariantCulture)),
    ("Line amounts", d => string.Join(" ", d.Lines.Select(l => $"{l.Quantity}x{l.UnitPrice}"))),
    ("VAT per line", d => string.Join(" ", d.Lines.Select(l => $"{l.VatCategoryCode}{l.VatPercentage}"))),
    ("Total excl.", d => d.Totals?.TaxExclusiveAmount?.ToString(CultureInfo.InvariantCulture)),
    ("VAT", d => d.Totals?.TaxAmount?.ToString(CultureInfo.InvariantCulture)),
    ("Payable", d => d.Totals?.PayableAmount?.ToString(CultureInfo.InvariantCulture)),
];

var report = new StringBuilder();
report.AppendLine($"# Model comparison, {DateTime.Now:yyyy-MM-dd HH:mm}");
report.AppendLine();
report.AppendLine($"{pdfs.Count} PDF(s); reference model: {models[0]}. A field marked ≠ differs from the reference.");
report.AppendLine();

var totals = models.ToDictionary(m => m, _ => (Cost: 0m, Seconds: 0.0, Differences: 0, Failures: 0));
var logger = new UsageLogger();

foreach (var path in pdfs)
{
    var pdf = await File.ReadAllBytesAsync(path);
    Console.WriteLine($"{Path.GetFileName(path)} ({pdf.Length / 1024} KB)");
    var results = new Dictionary<string, CanonicalInvoiceDraft?>();

    report.AppendLine($"## {Path.GetFileName(path)}");
    report.AppendLine();

    foreach (var model in models)
    {
        var interpreter = new AnthropicInvoiceInterpreter(
            new EnvironmentKey(),
            Options.Create(new AnthropicOptions { Model = model }),
            logger,
            null);
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await interpreter.InterpretAsync(pdf, null, model);
            watch.Stop();
            results[model] = result.Draft;
            var (input, output) = logger.Last;
            var price = prices.GetValueOrDefault(model);
            var cost = (input * price.Input + output * price.Output) / 1_000_000m;
            var t = totals[model];
            totals[model] = (t.Cost + cost, t.Seconds + watch.Elapsed.TotalSeconds, t.Differences, t.Failures);
            Console.WriteLine($"  {model,-18} {watch.Elapsed.TotalSeconds,5:0.0}s  {input,6} in  {output,6} out  ${cost:0.0000}");
        }
        catch (Exception exception)
        {
            results[model] = null;
            var t = totals[model];
            totals[model] = (t.Cost, t.Seconds, t.Differences, t.Failures + 1);
            Console.WriteLine($"  {model,-18} FAILED: {exception.Message}");
        }
    }

    report.AppendLine($"| Field | {string.Join(" | ", models)} |");
    report.AppendLine($"|---|{string.Join("|", models.Select(_ => "---"))}|");
    var reference = results[models[0]];
    foreach (var (name, read) in fields)
    {
        var expected = reference is null ? null : read(reference);
        var cells = models.Select(model =>
        {
            var draft = results[model];
            if (draft is null)
            {
                return "*failed*";
            }

            var value = read(draft);
            var differs = model != models[0] && !string.Equals(Normalise(value), Normalise(expected), StringComparison.OrdinalIgnoreCase);
            if (differs)
            {
                var t = totals[model];
                totals[model] = (t.Cost, t.Seconds, t.Differences + 1, t.Failures);
            }

            return (differs ? "≠ " : "") + (string.IsNullOrEmpty(value) ? "–" : value.Replace("|", "/", StringComparison.Ordinal));
        });
        report.AppendLine($"| {name} | {string.Join(" | ", cells)} |");
    }

    report.AppendLine();
}

report.AppendLine("## Summary");
report.AppendLine();
report.AppendLine("| Model | Cost (all PDFs) | Per PDF | Seconds per PDF | Fields ≠ reference | Failed |");
report.AppendLine("|---|---|---|---|---|---|");
foreach (var model in models)
{
    var t = totals[model];
    report.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"| {model} | ${t.Cost:0.0000} | ${t.Cost / pdfs.Count:0.0000} | {t.Seconds / pdfs.Count:0.0} | {t.Differences} | {t.Failures} |"));
}

var reportPath = Path.Combine(folder, "model-comparison.md");
await File.WriteAllTextAsync(reportPath, report.ToString());
Console.WriteLine();
Console.WriteLine(report.ToString()[report.ToString().IndexOf("## Summary", StringComparison.Ordinal)..]);
Console.WriteLine($"Report: {reportPath}");
return 0;

static string Normalise(string? value) => new string((value ?? "").Where(char.IsLetterOrDigit).ToArray());

sealed class EnvironmentKey : IAnthropicApiKeyProvider
{
    public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!);

    public void Invalidate()
    {
    }
}

/// <summary>Picks the token counts out of the interpreter's usage log line.</summary>
sealed class UsageLogger : ILogger<AnthropicInvoiceInterpreter>
{
    public (long Input, long Output) Last { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (state is IReadOnlyList<KeyValuePair<string, object?>> values
            && values.FirstOrDefault(v => v.Key == "InputTokens").Value is { } input
            && values.FirstOrDefault(v => v.Key == "OutputTokens").Value is { } output)
        {
            Last = (Convert.ToInt64(input, CultureInfo.InvariantCulture), Convert.ToInt64(output, CultureInfo.InvariantCulture));
        }
    }
}
