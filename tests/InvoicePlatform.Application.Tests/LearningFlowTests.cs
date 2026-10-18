using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// Upload, correct, book, upload again: the second invoice comes back with
/// the corrections, and the model gets the reading hints.
/// </summary>
public class LearningFlowTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeLearningStore _store = new();
    private readonly FakeInvoiceStore _invoices = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-19T09:00:00Z"));
    private readonly ScriptedInterpreter _interpreter = new();
    private readonly FakePlatformStore _platform = new();
    private readonly FakeUsageMeter _usage = new();
    private readonly ExtractInvoiceService _extract;
    private readonly BookInvoiceService _book;

    public LearningFlowTests()
    {
        var learning = new LearningService(_store, _clock);
        var relations = new RelationService(new FakeRelationStore(), _clock, new InvoiceRulesetRegistry());
        _extract = new ExtractInvoiceService(
            _interpreter, new InvoiceEnrichment(_settings, relations), new InvoiceCalculationValidator(), new CanonicalInvoiceXmlSerializer(), learning, _platform, _usage);
        _book = new BookInvoiceService(_invoices, _invoices, _clock, relations, learning);
    }

    /// <summary>The model misses the seller's VAT number and the buyer reference.</summary>
    private static CanonicalInvoiceDraft ModelReading() => CompleteInvoice.Create() with
    {
        Seller = CompleteInvoice.Create().Seller! with { VatIdentifier = null },
        BuyerReference = null,
    };

    private async Task UploadCorrectAndBook(string organizationId = OrgA)
    {
        _interpreter.Next = ModelReading();
        var extracted = await _extract.ExtractAsync(organizationId, new byte[] { 1 });
        await _book.BookAsync(organizationId, "u", CompleteInvoice.Create(), new byte[] { 1 }, "a.pdf", "m", extractionId: extracted.InvoiceId);
    }

    [Fact]
    public async Task The_next_upload_gets_the_corrected_own_details_back_with_a_notice()
    {
        await UploadCorrectAndBook();

        _interpreter.Next = ModelReading();
        var second = await _extract.ExtractAsync(OrgA, new byte[] { 1 });

        Assert.Equal(CompleteInvoice.Create().Seller!.VatIdentifier, second.Draft.Seller!.VatIdentifier);
        Assert.Contains(second.Warnings, w => w is { Code: "LEARNED", Path: "seller.vatIdentifier", Severity: ValidationSeverity.Info });
    }

    [Fact]
    public async Task The_model_gets_the_header_corrections_as_hints()
    {
        await UploadCorrectAndBook();

        _interpreter.Next = ModelReading();
        await _extract.ExtractAsync(OrgA, new byte[] { 1 });

        Assert.Contains(_interpreter.LastHints!, h => h is { Field: "buyerReference", YouRead: null } && h.ReviewerEntered == CompleteInvoice.Create().BuyerReference);
    }

    [Fact]
    public async Task One_organisations_corrections_never_reach_another()
    {
        await UploadCorrectAndBook(OrgA);

        _interpreter.Next = ModelReading();
        var other = await _extract.ExtractAsync(OrgB, new byte[] { 1 });

        Assert.Null(other.Draft.Seller!.VatIdentifier);
        Assert.Empty(_interpreter.LastHints!);
    }

    [Fact]
    public async Task Booking_the_same_upload_twice_teaches_once()
    {
        _interpreter.Next = ModelReading();
        var extracted = await _extract.ExtractAsync(OrgA, new byte[] { 1 });
        var first = await _book.BookAsync(OrgA, "u", CompleteInvoice.Create(), new byte[] { 1 }, "a.pdf", "m", extractionId: extracted.InvoiceId);
        await _book.BookAsync(OrgA, "u", CompleteInvoice.Create(), new byte[] { 1 }, "a.pdf", "m", first.InvoiceId, extracted.InvoiceId);

        Assert.All(await _store.GetCorrectionsAsync(OrgA), c => Assert.Equal(1, c.Count));
    }

    [Fact]
    public async Task Undoing_an_applied_correction_makes_xchange_forget_it()
    {
        await UploadCorrectAndBook();

        _interpreter.Next = ModelReading();
        var second = await _extract.ExtractAsync(OrgA, new byte[] { 1 });
        var undone = second.Draft with { Seller = second.Draft.Seller! with { VatIdentifier = null } };
        await _book.BookAsync(OrgA, "u", undone, new byte[] { 1 }, "a.pdf", "m", extractionId: second.InvoiceId);

        Assert.DoesNotContain(await _store.GetCorrectionsAsync(OrgA), c => c.Field == "seller.vatIdentifier");
    }

    [Fact]
    public async Task A_correction_can_be_forgotten_by_hand_but_not_another_organisations()
    {
        await UploadCorrectAndBook();
        var learning = new LearningService(_store, _clock);
        var id = (await learning.ListAsync(OrgA))[0].Id;

        Assert.False(await learning.ForgetAsync(OrgB, id));
        Assert.True(await learning.ForgetAsync(OrgA, id));
        Assert.DoesNotContain(await learning.ListAsync(OrgA), c => c.Id == id);
    }

    [Fact]
    public async Task The_platform_administrators_model_reads_the_pdf()
    {
        _interpreter.Next = ModelReading();
        await _extract.ExtractAsync(OrgA, new byte[] { 1 });
        Assert.Null(_interpreter.LastModel);

        await _platform.SaveSettingsAsync(new InvoicePlatform.Application.Organizations.PlatformSettings { ExtractionModel = "claude-opus-5-5" });
        await _extract.ExtractAsync(OrgA, new byte[] { 1 });

        Assert.Equal("claude-opus-5-5", _interpreter.LastModel);
    }

    private sealed class ScriptedInterpreter : IInvoiceInterpreter
    {
        public CanonicalInvoiceDraft Next { get; set; } = new();

        public IReadOnlyList<ReadingHint>? LastHints { get; private set; }

        public string? LastModel { get; private set; }

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
        {
            LastHints = hints;
            LastModel = model;
            return Task.FromResult(new InvoiceInterpretationResult(Next, new ProviderExecution("test", "m", 0)));
        }
    }
}
