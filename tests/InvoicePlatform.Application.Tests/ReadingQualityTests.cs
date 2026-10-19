using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Quality;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoicePlatform.Application.Tests;

/// <summary>Upload, review, book: the booking is measured against what was read.</summary>
public class ReadingQualityTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeLearningStore _store = new();
    private readonly FakeInvoiceStore _invoices = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeQualityLedger _quality = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-19T09:00:00Z"));
    private readonly Interpreter _interpreter = new();
    private readonly ExtractInvoiceService _extract;
    private readonly BookInvoiceService _book;

    public ReadingQualityTests()
    {
        var learning = new LearningService(_store, _clock);
        var relations = new RelationService(new FakeRelationStore(), _clock, new InvoiceRulesetRegistry());
        _extract = new ExtractInvoiceService(
            _interpreter, new InvoiceEnrichment(_settings, relations), new InvoiceCalculationValidator(), new CanonicalInvoiceXmlSerializer(),
            learning, new FakePlatformStore(), new FakeUsageMeter(), new FakeUsageLedger(), _clock, NullLogger<ExtractInvoiceService>.Instance);
        _book = new BookInvoiceService(_invoices, _invoices, _clock, relations, learning,
            new ReadingQualityService(_quality, _clock, NullLogger<ReadingQualityService>.Instance));
    }

    /// <summary>What the reviewer books: the complete invoice, with its own arithmetic printed on it.</summary>
    private static CanonicalInvoiceDraft Booked() => InvoiceTotalsCalculator.Apply(CompleteInvoice.Create());

    /// <summary>The model got the invoice number wrong and missed the buyer reference.</summary>
    private static CanonicalInvoiceDraft Misread() => Booked() with { InvoiceNumber = "F-2026-014", BuyerReference = null };

    private async Task<string> UploadAsync(string organizationId, CanonicalInvoiceDraft reading)
    {
        _interpreter.Next = reading;
        return (await _extract.ExtractAsync(organizationId, new byte[] { 1 }, "user-1", UsageSources.Upload)).InvoiceId;
    }

    private Task<Domain.Invoices.StoredInvoice> BookAsync(string organizationId, string extractionId, string? invoiceId = null, bool reviewed = true)
        => _book.BookAsync(organizationId, "user-1", Booked(), new byte[] { 1 }, "a.pdf", "client-says-this", invoiceId, extractionId, reviewed);

    [Fact]
    public async Task A_reviewed_booking_is_measured_with_who_what_and_which_model()
    {
        _interpreter.Model = "claude-sonnet-5-5";
        var extractionId = await UploadAsync(OrgA, Misread());

        var stored = await BookAsync(OrgA, extractionId);

        var measured = Assert.Single(_quality.All);
        Assert.Equal(OrgA, measured.OrganizationId);
        Assert.Equal(extractionId, measured.ExtractionId);
        Assert.Equal(stored.InvoiceId, measured.InvoiceId);
        Assert.Equal("claude-sonnet-5-5", measured.Model); // from the extraction, not from the client
        Assert.Equal("user-1", measured.Actor);
        Assert.Equal(_clock.UtcNow, measured.At);
        Assert.False(measured.Read.Perfect);
        Assert.Equal(
            [("invoiceNumber", FieldOutcome.Corrected), ("buyerReference", FieldOutcome.Missed)],
            measured.Read.NotCorrect.Select(f => (f.Path, f.Outcome)));
    }

    [Fact]
    public async Task Learning_does_not_count_for_the_model_but_does_for_what_the_reviewer_was_shown()
    {
        // Book a correction of the seller's VAT number once; the next upload gets it applied.
        var reading = Booked() with { Seller = Booked().Seller! with { VatIdentifier = "NL000000000B00" } };
        await BookAsync(OrgA, await UploadAsync(OrgA, reading));

        var second = await UploadAsync(OrgA, reading);
        await BookAsync(OrgA, second);

        var measured = _quality.All[^1];
        Assert.Equal(FieldOutcome.Corrected, Assert.Single(measured.Read.NotCorrect).Outcome);
        Assert.True(measured.Shown.Perfect);
    }

    [Fact]
    public async Task What_xchange_filled_from_the_settings_is_not_held_against_the_model()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = OrgA, EndpointId = "12345678", EndpointScheme = "0106" });
        var reading = Booked() with { Seller = Booked().Seller! with { ElectronicAddress = null, ElectronicAddressScheme = null } };

        await BookAsync(OrgA, await UploadAsync(OrgA, reading));

        var measured = Assert.Single(_quality.All);
        Assert.True(measured.Read.Perfect, string.Join(", ", measured.Read.NotCorrect.Select(f => f.Path)));
        Assert.DoesNotContain(measured.Read.Fields, f => f.Path == "seller.electronicAddress");
        Assert.Contains(measured.Shown.Fields, f => f is { Path: "seller.electronicAddress", Outcome: FieldOutcome.Correct });
    }

    [Fact]
    public async Task Only_the_first_booking_of_an_upload_is_measured()
    {
        var extractionId = await UploadAsync(OrgA, Misread());

        var first = await BookAsync(OrgA, extractionId);
        await BookAsync(OrgA, extractionId, first.InvoiceId);

        Assert.Single(_quality.All);
    }

    [Fact]
    public async Task A_booking_nobody_reviewed_is_not_measured_but_still_teaches()
    {
        var extractionId = await UploadAsync(OrgA, Misread());

        await BookAsync(OrgA, extractionId, reviewed: false);

        Assert.Empty(_quality.All);
        Assert.NotEmpty(await _store.GetCorrectionsAsync(OrgA)); // the buyer reference hint was learned
    }

    [Fact]
    public async Task A_booking_without_an_upload_is_not_measured()
    {
        await _book.BookAsync(OrgA, "user-1", Booked(), new byte[] { 1 }, "a.pdf", "m", reviewed: true);

        Assert.Empty(_quality.All);
    }

    [Fact]
    public async Task A_failing_ledger_never_fails_the_booking()
    {
        _quality.Fail = true;
        var extractionId = await UploadAsync(OrgA, Misread());

        var stored = await BookAsync(OrgA, extractionId);

        Assert.NotNull(await _invoices.GetAsync(OrgA, stored.InvoiceId));
    }

    [Fact]
    public async Task Another_organisation_cannot_book_against_an_upload_or_be_measured_by_it()
    {
        var extractionId = await UploadAsync(OrgA, Misread());

        await BookAsync(OrgB, extractionId);
        Assert.Empty(_quality.All);

        await BookAsync(OrgA, extractionId);
        Assert.Single(_quality.Of(OrgA));
        Assert.Empty(_quality.Of(OrgB));
    }

    [Fact]
    public async Task A_snapshot_kept_before_the_model_was_recorded_is_not_measured()
    {
        await _store.SaveExtractionAsync(OrgA, new ExtractionSnapshot("old", Misread(), [], _clock.UtcNow));

        await BookAsync(OrgA, "old");

        Assert.Empty(_quality.All);
    }

    private sealed class Interpreter : IInvoiceInterpreter
    {
        public CanonicalInvoiceDraft Next { get; set; } = new();

        public string Model { get; set; } = "claude-haiku-5-5";

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new InvoiceInterpretationResult(Next, new ProviderExecution("test", Model, 0, TokenUsage.None)));
    }
}

public class QualityReportTests
{
    private static readonly UsageMonth October = new(2026, 10);

    private readonly FakeQualityLedger _quality = new();
    private readonly FakeUsageLedger _costs = new();
    private readonly FakePlatformStore _platform = new();
    private readonly QualityReportService _report;

    public QualityReportTests() => _report = new QualityReportService(_quality, _costs, _platform);

    private static ReadingComparison Reading(params FieldOutcome[] outcomes)
        => new([.. outcomes.Select((o, i) => new FieldComparison($"f{i}", i == 0 ? "invoiceNumber" : "lines[].netAmount", o))]);

    private Task MeasureAsync(string organizationId, string model, ReadingComparison read, ReadingComparison? shown = null, string at = "2026-10-05T10:00:00Z")
        => _quality.RecordAsync(new QualityMeasurement
        {
            MeasurementId = Guid.NewGuid().ToString("n"),
            OrganizationId = organizationId,
            ExtractionId = "x",
            InvoiceId = "i",
            Model = model,
            Actor = "u",
            At = DateTimeOffset.Parse(at),
            Read = read,
            Shown = shown ?? read,
        });

    private Task CostAsync(string model, decimal cost) => _costs.RecordAsync(new UsageEvent
    {
        EventId = Guid.NewGuid().ToString("n"),
        OrganizationId = "org-a",
        Actor = "u",
        Source = UsageSources.Upload,
        ExtractionId = "x",
        Model = model,
        Tokens = TokenUsage.None,
        CostUsd = cost,
        PriceVersion = ModelPrices.Version,
        PriceKnown = true,
        Succeeded = true,
        At = DateTimeOffset.Parse("2026-10-04T10:00:00Z"),
    });

    [Fact]
    public async Task The_month_gives_accuracy_and_perfect_invoices_in_percent_with_decimals()
    {
        await MeasureAsync("org-a", "claude-haiku-5-5", Reading(FieldOutcome.Correct, FieldOutcome.Correct, FieldOutcome.Correct));
        await MeasureAsync("org-a", "claude-haiku-5-5", Reading(FieldOutcome.Correct, FieldOutcome.Missed, FieldOutcome.Corrected));
        await MeasureAsync("org-b", "claude-haiku-5-5", Reading(FieldOutcome.Correct));
        await MeasureAsync("org-b", "claude-haiku-5-5", Reading(FieldOutcome.Extra), at: "2026-11-01T00:00:00Z");

        var report = await _report.MonthAsync(October);

        Assert.Equal(3, report.Total.Read.Invoices);
        Assert.Equal(2, report.Total.Read.PerfectInvoices);
        Assert.Equal(66.7m, report.Total.Read.PerfectPercent);
        Assert.Equal(71.4m, report.Total.Read.FieldAccuracyPercent); // 5 of 7
        Assert.Equal(["org-a", "org-b"], report.Organizations.Select(o => o.Key));
    }

    [Fact]
    public async Task Cost_per_perfect_invoice_is_the_models_cost_over_its_perfect_invoices()
    {
        await CostAsync("claude-haiku-5-5", 0.003m);
        await CostAsync("claude-haiku-5-5", 0.004m);
        await CostAsync("claude-opus-5-5", 0.09m);
        await MeasureAsync("org-a", "claude-haiku-5-5", Reading(FieldOutcome.Correct));
        await MeasureAsync("org-a", "claude-haiku-5-5", Reading(FieldOutcome.Correct));
        await MeasureAsync("org-a", "claude-opus-5-5", Reading(FieldOutcome.Missed));

        var report = await _report.MonthAsync(October);

        var haiku = Assert.Single(report.Models, m => m.Key == "claude-haiku-5-5");
        var opus = Assert.Single(report.Models, m => m.Key == "claude-opus-5-5");
        Assert.Equal(0.0035m, haiku.CostPerPerfectInvoiceUsd);
        Assert.Equal(0.09m, opus.CostUsd);
        Assert.Null(opus.CostPerPerfectInvoiceUsd); // no perfect invoice: shown as a dash
        Assert.Equal(0.0485m, report.CostPerPerfectInvoiceUsd);
    }

    [Fact]
    public async Task The_fields_that_go_wrong_most_come_first_and_correct_ones_are_left_out()
    {
        await MeasureAsync("org-a", "m", Reading(FieldOutcome.Correct, FieldOutcome.Corrected, FieldOutcome.Missed));
        await MeasureAsync("org-a", "m", Reading(FieldOutcome.Correct, FieldOutcome.Correct));

        var report = await _report.MonthAsync(October);

        var field = Assert.Single(report.Fields);
        Assert.Equal("lines[].netAmount", field.Field);
        Assert.Equal((3, 1, 1, 1, 0), (field.Sums.Read.Counted, field.Sums.Read.Correct, field.Sums.Read.Corrected, field.Sums.Read.Missed, field.Sums.Read.Extra));
    }

    [Fact]
    public async Task An_empty_month_has_no_percentages_rather_than_zero()
    {
        var report = await _report.MonthAsync(October);

        Assert.Null(report.Total.Read.FieldAccuracyPercent);
        Assert.Null(report.Total.Read.PerfectPercent);
        Assert.Null(report.CostPerPerfectInvoiceUsd);
    }
}
