using InvoicePlatform.Application.Conversions;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Invoices;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// The conversion API's flow: a PDF sent with a key is read by the worker,
/// then booked without a person when nothing stands in the way, and otherwise
/// waits in the review list until someone books it.
/// </summary>
public class ConversionServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private static readonly byte[] Pdf = "%PDF-1.4"u8.ToArray();
    private static readonly ApiKeyCaller Caller = new(OrgA, "k1");

    private readonly Imports _imports = new();
    private readonly FakeInvoiceStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-18T09:00:00Z"));
    private readonly Interpreter _interpreter = new();
    private readonly FakeOfficialValidator _official = new();
    private readonly FakeUsageMeter _usage = new();
    private readonly ImportService _importService;
    private readonly BookInvoiceService _booking;
    private readonly ConversionService _service;

    public ConversionServiceTests()
    {
        var settings = new FakeSettingsStore();
        var registry = new InvoiceRulesetRegistry();
        var relations = new RelationService(new FakeRelationStore(), _clock, registry);
        var learning = new LearningService(new FakeLearningStore(), _clock);
        var enrichment = new InvoiceEnrichment(settings, relations);
        var extraction = new ExtractInvoiceService(
            _interpreter, enrichment, new InvoiceCalculationValidator(), new CanonicalInvoiceXmlSerializer(),
            learning, new FakePlatformStore(), _usage);
        var export = new ExportInvoiceService(
            _store, settings, registry, enrichment, new InvoicePlatform.Domain.EInvoicing.Ubl.UblInvoiceWriter(),
            _official, new FakePlatformStore(), _usage);
        _booking = new BookInvoiceService(_store, _store, _clock, relations, learning);
        _importService = new ImportService(
            _imports, _imports, _store, extraction, new ConversionCompletion(export, _booking, _store, _clock), _clock);
        _service = new ConversionService(_importService, _store, registry, export, _usage);
    }

    /// <summary>A draft as a model reads a document whose printed figures add up.</summary>
    private static CanonicalInvoiceDraft Printed(CanonicalInvoiceDraft draft) => InvoiceTotalsCalculator.Apply(draft);

    private async Task<string> SendAndRead(string? format = null)
    {
        var submission = await _service.SubmitAsync(Caller, format, "f.pdf", Pdf);
        var id = submission.Conversion!.ConversionId;
        await _importService.ProcessAsync(OrgA, id);
        return id;
    }

    [Fact]
    public async Task A_conversion_is_queued_at_once_without_approval()
    {
        var submission = await _service.SubmitAsync(Caller, null, "f.pdf", Pdf);

        Assert.True(submission.FormatFound);
        Assert.Equal(ConversionStatus.Queued, submission.Conversion!.Status);
        Assert.Equal((OrgA, submission.Conversion.ConversionId), Assert.Single(_imports.Queued));

        var record = (await _imports.GetAsync(OrgA, submission.Conversion.ConversionId))!;
        Assert.Equal(ImportSource.Api, record.Source);
        Assert.Equal("apikey:k1", record.CreatedByUserId);
    }

    [Fact]
    public async Task An_unknown_format_is_refused_and_nothing_is_stored()
    {
        var submission = await _service.SubmitAsync(Caller, "xrechnung-9", "f.pdf", Pdf);

        Assert.False(submission.FormatFound);
        Assert.Empty(_imports.Queued);
        Assert.Empty(_store.Documents);
    }

    [Fact]
    public async Task Without_errors_it_is_booked_by_the_key_and_leaves_the_review_list()
    {
        var id = await SendAndRead();

        var invoice = (await _store.GetAsync(OrgA, id))!;
        Assert.Equal(InvoiceStatus.Booked, invoice.Status);
        Assert.Equal("apikey:k1", invoice.BookedByUserId);
        Assert.Equal($"{OrgA}/{id}.pdf", invoice.DocumentKey);
        Assert.Empty(await _importService.ListAsync(OrgA));

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.Ready, conversion.Status);
        Assert.Equal("F-2026-041", conversion.InvoiceNumber);
        // Nothing was asked for: the format that was checked is kept.
        Assert.Equal(PeppolBisBilling30Ruleset.RulesetId, conversion.Format);
        Assert.Empty(conversion.Violations);
    }

    [Fact]
    public async Task A_ready_conversion_hands_out_the_exported_file()
    {
        var id = await SendAndRead(PeppolBisBilling30Ruleset.RulesetId);

        var file = await _service.GetFileAsync(OrgA, id);

        Assert.Equal(ConversionFileKind.Ready, file.Kind);
        Assert.Equal("F-2026-041-peppol-bis-billing-3.0.xml", file.Export!.FileName);
    }

    [Fact]
    public async Task With_errors_it_waits_for_review_with_its_violations()
    {
        _interpreter.Draft = Printed(CompleteInvoice.Create() with { BuyerReference = null });

        var id = await SendAndRead();

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.NeedsReview, conversion.Status);
        Assert.Contains(conversion.Violations, v => v.RuleId == "PEPPOL-EN16931-R003");
        Assert.Null(await _store.GetAsync(OrgA, id));
        Assert.Equal(id, Assert.Single(await _importService.ListAsync(OrgA)).ImportId);
        Assert.Equal(ConversionFileKind.NotReady, (await _service.GetFileAsync(OrgA, id)).Kind);
    }

    [Fact]
    public async Task Once_a_person_books_it_in_the_app_the_conversion_is_ready()
    {
        _interpreter.Draft = Printed(CompleteInvoice.Create() with { BuyerReference = null });
        var id = await SendAndRead();
        var record = (await _imports.GetAsync(OrgA, id))!;

        // What the review screen does: book under the import id, then take it off the list.
        await _booking.BookAsync(OrgA, "user-a", record.Draft! with { BuyerReference = "PO-1" }, Pdf, record.FileName, "test/m", invoiceId: id);
        Assert.True(await _importService.DismissAsync(OrgA, id));

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.Ready, conversion.Status);
        Assert.Empty(conversion.Violations);
        Assert.Empty(await _importService.ListAsync(OrgA));
        Assert.Equal(ConversionFileKind.Ready, (await _service.GetFileAsync(OrgA, id)).Kind);
    }

    [Fact]
    public async Task Dismissed_without_booking_it_fails()
    {
        _interpreter.Draft = Printed(CompleteInvoice.Create() with { BuyerReference = null });
        var id = await SendAndRead();

        await _importService.DismissAsync(OrgA, id);

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.Failed, conversion.Status);
        Assert.NotNull(conversion.Error);
    }

    [Fact]
    public async Task A_printed_total_that_does_not_add_up_is_for_a_person()
    {
        var draft = Printed(CompleteInvoice.Create());
        _interpreter.Draft = draft with { Lines = [draft.Lines[0] with { NetAmount = 999.99m }] };

        var id = await SendAndRead();

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.NeedsReview, conversion.Status);
        Assert.Contains(conversion.Violations, v => v.RuleId == "CALC-LINE-NET");
    }

    [Fact]
    public async Task Without_the_official_verdict_nothing_is_booked_unseen()
    {
        _official.Verdict = OfficialValidation.Unavailable("down");

        var id = await SendAndRead();

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.NeedsReview, conversion.Status);
        Assert.Equal("XC-OFFICIAL-UNAVAILABLE", Assert.Single(conversion.Violations).RuleId);
    }

    [Fact]
    public async Task An_unreadable_pdf_fails()
    {
        _interpreter.Fail = true;

        var id = await SendAndRead();

        var conversion = (await _service.GetAsync(OrgA, id))!;
        Assert.Equal(ConversionStatus.Failed, conversion.Status);
        Assert.Contains("unreadable", conversion.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trial_converts_but_gets_no_file()
    {
        await _usage.SetLimitAsync(OrgA, 10, trial: true);
        var id = await SendAndRead();

        Assert.Equal(ConversionStatus.Ready, (await _service.GetAsync(OrgA, id))!.Status);
        var file = await _service.GetFileAsync(OrgA, id);
        Assert.Equal(ConversionFileKind.Refused, file.Kind);
        Assert.Equal(ExportOutcomeKind.PlanRequired, file.Export!.Kind);
    }

    [Fact]
    public async Task A_conversion_is_invisible_to_another_organisation()
    {
        var id = await SendAndRead();

        Assert.Null(await _service.GetAsync(OrgB, id));
        Assert.Equal(ConversionFileKind.NotFound, (await _service.GetFileAsync(OrgB, id)).Kind);
    }

    [Fact]
    public async Task An_ordinary_upload_is_not_a_conversion()
    {
        var upload = await _importService.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "user-a");
        await _importService.ProcessAsync(OrgA, upload.ImportId);

        Assert.Null(await _service.GetAsync(OrgA, upload.ImportId));
        // Nor is an upload booked by itself.
        Assert.Null(await _store.GetAsync(OrgA, upload.ImportId));
        Assert.Equal(ImportStatus.Ready, (await _imports.GetAsync(OrgA, upload.ImportId))!.Status);
    }

    private sealed class Interpreter : IInvoiceInterpreter
    {
        public bool Fail { get; set; }

        /// <summary>As printed: totals and VAT breakdown present and adding up.</summary>
        public CanonicalInvoiceDraft Draft { get; set; } = Printed(CompleteInvoice.Create());

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => Fail
                ? throw new InvalidOperationException("The document is unreadable.")
                : Task.FromResult(new InvoiceInterpretationResult(Draft, new ProviderExecution("test", "m", 0)));
    }

    private sealed class Imports : IImportRepository, IImportQueue
    {
        private readonly Dictionary<(string Org, string Id), ImportRecord> _records = [];

        public List<(string Org, string Id)> Queued { get; } = [];

        public Task SaveAsync(ImportRecord record, CancellationToken cancellationToken = default)
        {
            _records[(record.OrganizationId, record.ImportId)] = record;
            return Task.CompletedTask;
        }

        public Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
            => Task.FromResult(_records.GetValueOrDefault((organizationId, importId)));

        public Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImportRecord>>(_records.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());

        public Task DeleteAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        {
            _records.Remove((organizationId, importId));
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        {
            Queued.Add((organizationId, importId));
            return Task.CompletedTask;
        }
    }
}
