using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoicePlatform.Application.Tests;

public class ModelPricesTests
{
    [Fact]
    public void Every_kind_of_token_has_its_own_price()
    {
        // Sonnet 5.5: $2 in, $10 out; cache write 1.25 x $2, cache read 0.1 x $2 (per million).
        var cost = ModelPrices.Cost("claude-sonnet-5-5", new TokenUsage(1_000, 500, 2_000, 10_000));

        // 1000x2 + 500x10 + 2000x2.5 + 10000x0.2 = 14000 per million.
        Assert.Equal(0.014m, cost.CostUsd);
        Assert.True(cost.PriceKnown);
        Assert.Equal(ModelPrices.Version, cost.PriceVersion);
    }

    [Theory]
    [InlineData("claude-haiku-5-5", 1, 0, "0")] // 0.0000001 rounds down
    [InlineData("claude-haiku-5-5", 5, 0, "0.000001")] // 0.0000005 rounds away from zero
    [InlineData("claude-opus-5", 0, 3, "0.000075")]
    [InlineData("claude-opus-5-5", 12_345, 678, "0.06294")]
    public void Costs_are_rounded_to_the_micro_dollar(string model, long input, long output, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ModelPrices.Cost(model, new TokenUsage(input, output, 0, 0)).CostUsd);
    }

    [Fact]
    public void A_model_without_a_price_costs_nothing_and_says_so()
    {
        var cost = ModelPrices.Cost("claude-unknown-9", new TokenUsage(1_000_000, 1_000_000, 0, 0));

        Assert.Equal(0m, cost.CostUsd);
        Assert.False(cost.PriceKnown);
    }

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("2026-1", false)]
    [InlineData("2026-13", false)]
    [InlineData("2026-00", false)]
    [InlineData("26-10-01", false)]
    [InlineData("1999-12", false)]
    [InlineData("2026/10", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_month_is_exactly_yyyy_MM(string? text, bool valid)
    {
        Assert.Equal(valid, UsageMonth.TryParse(text, out var month));
        if (valid)
        {
            Assert.Equal(text, month.ToString());
        }
    }
}

public class CostLedgerTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeUsageLedger _ledger = new();
    private readonly FakeUsageMeter _usage = new();
    private readonly FakePlatformStore _platform = new();
    private readonly AuthServiceTests.FakeStore _users = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-19T09:00:00Z"));
    private readonly Interpreter _interpreter = new();
    private readonly ExtractInvoiceService _extract;
    private readonly CostReportService _report;

    public CostLedgerTests()
    {
        var relations = new RelationService(new FakeRelationStore(), _clock, new InvoiceRulesetRegistry());
        _extract = new ExtractInvoiceService(
            _interpreter,
            new InvoiceEnrichment(new FakeSettingsStore(), relations),
            new InvoiceCalculationValidator(),
            new CanonicalInvoiceXmlSerializer(),
            new LearningService(new FakeLearningStore(), _clock),
            _platform,
            _usage,
            _ledger,
            _clock,
            NullLogger<ExtractInvoiceService>.Instance);
        _report = new CostReportService(_ledger, _platform, _users, _clock);
    }

    private static readonly UsageMonth October = new(2026, 10);

    [Fact]
    public async Task Every_extraction_is_recorded_with_who_where_which_model_and_what_it_cost()
    {
        _interpreter.Usage = new TokenUsage(1_000, 500, 2_000, 10_000);
        _interpreter.Model = "claude-sonnet-5-5";

        var result = await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);

        var recorded = Assert.Single(_ledger.All);
        Assert.Equal(OrgA, recorded.OrganizationId);
        Assert.Equal("user-1", recorded.Actor);
        Assert.Equal(UsageSources.Upload, recorded.Source);
        Assert.Equal(result.InvoiceId, recorded.ExtractionId);
        Assert.Equal("claude-sonnet-5-5", recorded.Model);
        Assert.Equal(new TokenUsage(1_000, 500, 2_000, 10_000), recorded.Tokens);
        Assert.Equal(0.014m, recorded.CostUsd);
        Assert.Equal(ModelPrices.Version, recorded.PriceVersion);
        Assert.True(recorded.PriceKnown);
        Assert.True(recorded.Succeeded);
        Assert.Equal(_clock.UtcNow, recorded.At);
    }

    [Fact]
    public async Task Each_call_adds_to_the_organisation_its_user_its_model_and_the_platform()
    {
        _interpreter.Model = "claude-haiku-5-5";
        _interpreter.Usage = new TokenUsage(10_000, 1_000, 0, 0); // 0.001 + 0.0005
        await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);
        await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-2", UsageSources.Upload);
        await _extract.ExtractAsync(OrgB, new byte[] { 1 }, "user-3", UsageSources.Upload);

        var platform = await _report.MonthAsync(October);
        var orgA = await _report.OrganizationMonthAsync(OrgA, October);

        Assert.Equal(3, platform.Total.Calls);
        Assert.Equal(0.0045m, platform.Total.CostUsd);
        Assert.Equal(0.0015m, platform.Total.AverageCostUsd);
        Assert.Equal(30_000, platform.Total.InputTokens);
        Assert.Equal(("2026-10-19", 3L), (Assert.Single(platform.Days).Key, platform.Days[0].Totals.Calls));
        Assert.Equal(("claude-haiku-5-5", 3L), (Assert.Single(platform.Models).Key, platform.Models[0].Totals.Calls));
        Assert.Equal([OrgA, OrgB], platform.Organizations.Select(o => o.Key));

        Assert.Equal(2, orgA.Total.Calls);
        Assert.Equal(["user-1", "user-2"], orgA.Users.Select(u => u.Actor).Order());
        Assert.Equal(2, orgA.TopEvents.Count);
    }

    [Fact]
    public async Task An_unpriced_model_is_recorded_at_zero_and_flagged_without_failing_the_extraction()
    {
        _interpreter.Model = "claude-next-1";

        var result = await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);

        Assert.Equal("F-2026-001", result.Draft.InvoiceNumber);
        var recorded = Assert.Single(_ledger.All);
        Assert.False(recorded.PriceKnown);
        Assert.Equal(0m, recorded.CostUsd);
    }

    [Fact]
    public async Task A_ledger_that_fails_does_not_fail_the_extraction()
    {
        _ledger.Fail = true;

        var result = await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);

        Assert.Equal("F-2026-001", result.Draft.InvoiceNumber);
        Assert.Equal(1, (await _usage.GetAsync(OrgA)).Used);
    }

    [Fact]
    public async Task A_paid_answer_that_could_not_be_used_is_recorded_as_a_failed_call()
    {
        _interpreter.FailWith = new InterpretationFailedException(
            "unusable", new ProviderExecution("test", "claude-opus-5", 10, new TokenUsage(2_000, 100, 0, 0)));

        await Assert.ThrowsAsync<InterpretationFailedException>(() => _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload));

        var recorded = Assert.Single(_ledger.All);
        Assert.False(recorded.Succeeded);
        Assert.Equal(0.0125m, recorded.CostUsd); // 2000x5 + 100x25 per million
        Assert.Equal(0, (await _usage.GetAsync(OrgA)).Used); // the conversion is still given back
        Assert.Equal(1, (await _report.MonthAsync(October)).Total.FailedCalls);
    }

    [Fact]
    public async Task A_failure_without_an_answer_records_nothing()
    {
        _interpreter.FailWith = new InterpretationFailedException("Anthropic rejected the API key");

        await Assert.ThrowsAsync<InterpretationFailedException>(() => _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload));

        Assert.Empty(_ledger.All);
    }

    [Fact]
    public async Task An_organisations_costs_are_not_readable_as_another_organisations()
    {
        await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);

        var asB = await _report.OrganizationMonthAsync(OrgB, October);

        Assert.Equal(0, asB.Total.Calls);
        Assert.Empty(asB.Users);
        Assert.Empty(asB.Models);
        Assert.Empty(asB.TopEvents);
        Assert.Empty(await _ledger.ListEventsAsync(OrgB, October));
    }

    [Fact]
    public async Task Imports_are_booked_to_their_uploader_or_to_email()
    {
        var imports = new ImportFakes();
        var documents = new FakeInvoiceStore();
        var service = new ImportService(imports, imports, documents, _extract, _clock);

        var batch = await service.SubmitAsync(OrgA, ImportSource.Upload, "a.pdf", "%PDF-1.4"u8.ToArray(), userId: "user-1");
        var mailed = await service.SubmitAsync(OrgA, ImportSource.Email, "b.pdf", "%PDF-1.4"u8.ToArray(), sender: "x@y");
        await service.ApproveAsync(OrgA, mailed.ImportId);
        await service.ProcessAsync(OrgA, batch.ImportId);
        await service.ProcessAsync(OrgA, mailed.ImportId);

        Assert.Equal(
            [("user-1", UsageSources.Batch), (UsageActors.Email, UsageSources.Email)],
            _ledger.All.Select(e => (e.Actor, e.Source)));
    }

    [Fact]
    public async Task The_detail_names_users_and_shows_the_twenty_most_expensive_calls()
    {
        await _users.AddAsync(new User { Id = "user-1", OrganizationId = OrgA, Email = "ann@a", PasswordHash = "-", Name = "Ann", Role = UserRole.User });
        await _platform.SaveOrganizationAsync(new Organization { OrganizationId = OrgA, Name = "Org A", CountryCode = "NL", CreatedAt = _clock.UtcNow });
        _interpreter.Model = "claude-haiku-5-5";

        for (var i = 1; i <= 25; i++)
        {
            _interpreter.Usage = new TokenUsage(i * 1_000, 0, 0, 0);
            await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);
        }

        await _extract.ExtractAsync(OrgA, new byte[] { 1 }, UsageActors.Email, UsageSources.Email);

        var detail = await _report.OrganizationMonthAsync(OrgA, October);

        Assert.Equal("Org A", detail.Name);
        var ann = detail.Users.Single(u => u.Actor == "user-1");
        Assert.Equal(("Ann", "ann@a"), (ann.Name, ann.Email));
        Assert.Null(detail.Users.Single(u => u.Actor == UsageActors.Email).Name);
        Assert.Equal(CostReportService.TopEventCount, detail.TopEvents.Count);
        Assert.Equal(0.0025m, detail.TopEvents[0].Event.CostUsd); // 25000 x 0.10 per million
        Assert.Equal("Ann", detail.TopEvents[0].ActorName);
        Assert.True(detail.TopEvents.Zip(detail.TopEvents.Skip(1)).All(p => p.First.Event.CostUsd >= p.Second.Event.CostUsd));
    }

    [Fact]
    public async Task The_csv_has_a_row_per_organisation_and_never_a_formula()
    {
        await _platform.SaveOrganizationAsync(new Organization { OrganizationId = OrgA, Name = "=HYPERLINK(\"x\"), Inc", CountryCode = "NL", CreatedAt = _clock.UtcNow });
        _interpreter.Model = "claude-opus-5";
        _interpreter.Usage = new TokenUsage(1_000, 0, 0, 0);
        await _extract.ExtractAsync(OrgA, new byte[] { 1 }, "user-1", UsageSources.Upload);
        await _extract.ExtractAsync(OrgB, new byte[] { 1 }, "user-2", UsageSources.Upload);

        var lines = (await _report.ExportCsvAsync(October)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("month,organizationId,organization,calls,", lines[0], StringComparison.Ordinal);
        Assert.Equal("2026-10,org-a,\"'=HYPERLINK(\"\"x\"\"), Inc\",1,0,1000,0,0,0,0.005,0.005,2026-10-18", lines[1]);
        Assert.Equal("2026-10,org-b,,1,0,1000,0,0,0,0.005,0.005,2026-10-18", lines[2]);
    }

    private sealed class Interpreter : IInvoiceInterpreter
    {
        public string Model { get; set; } = "claude-haiku-5-5";

        public TokenUsage Usage { get; set; } = new(1_000, 100, 0, 0);

        public Exception? FailWith { get; set; }

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => FailWith is not null
                ? Task.FromException<InvoiceInterpretationResult>(FailWith)
                : Task.FromResult(new InvoiceInterpretationResult(
                    CompleteInvoice.Create("F-2026-001"), new ProviderExecution("test", Model, 1, Usage)));
    }

    private sealed class ImportFakes : IImportRepository, IImportQueue
    {
        private readonly Dictionary<(string, string), ImportRecord> _records = [];

        public Task SaveAsync(ImportRecord record, CancellationToken cancellationToken = default)
        {
            _records[(record.OrganizationId, record.ImportId)] = record;
            return Task.CompletedTask;
        }

        public Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
            => Task.FromResult(_records.GetValueOrDefault((organizationId, importId)));

        public Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImportRecord>>([.. _records.Values.Where(r => r.OrganizationId == organizationId)]);

        public Task DeleteAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        {
            _records.Remove((organizationId, importId));
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(string organizationId, string importId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
