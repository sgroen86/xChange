using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class ImportServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeImports _imports = new();
    private readonly FakeInvoiceStore _documents = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-19T09:00:00Z"));
    private readonly Interpreter _interpreter = new();
    private readonly ImportService _service;

    public ImportServiceTests()
    {
        var relations = new RelationService(new FakeRelationStore(), _clock, new InvoiceRulesetRegistry());
        var extraction = new ExtractInvoiceService(
            _interpreter,
            new InvoiceEnrichment(new FakeSettingsStore(), relations),
            new InvoiceCalculationValidator(),
            new CanonicalInvoiceXmlSerializer(),
            new LearningService(new FakeLearningStore(), _clock), new FakePlatformStore(), new FakeUsageMeter());
        _service = new ImportService(_imports, _imports, _documents, extraction, TestConversions.Completion(_documents, _clock), _clock);
    }

    private static readonly byte[] Pdf = "%PDF-1.4"u8.ToArray();

    [Fact]
    public async Task A_submitted_pdf_is_stored_recorded_and_queued()
    {
        var record = await _service.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "u");

        Assert.Equal(ImportStatus.Queued, record.Status);
        Assert.Equal((OrgA, record.ImportId), Assert.Single(_imports.Queued));
        Assert.NotNull(await _documents.GetAsync(record.DocumentKey));
    }

    [Fact]
    public async Task Processing_reads_the_pdf_and_leaves_the_draft_ready()
    {
        var record = await _service.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "u");

        await _service.ProcessAsync(OrgA, record.ImportId);

        var ready = (await _service.GetAsync(OrgA, record.ImportId))!;
        Assert.Equal(ImportStatus.Ready, ready.Status);
        Assert.Equal("F-2026-001", ready.Draft!.InvoiceNumber);
        Assert.NotNull(ready.ExtractionId);
        Assert.Equal("test/m", ready.ProviderModel);
    }

    [Fact]
    public async Task A_document_the_model_cannot_read_fails_on_the_import_and_can_be_retried()
    {
        var record = await _service.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "u");
        _interpreter.Fail = true;

        await _service.ProcessAsync(OrgA, record.ImportId);
        var failed = (await _service.GetAsync(OrgA, record.ImportId))!;

        Assert.Equal(ImportStatus.Failed, failed.Status);
        Assert.Contains("unreadable", failed.Error, StringComparison.Ordinal);

        Assert.True(await _service.RetryAsync(OrgA, record.ImportId));
        Assert.Equal(ImportStatus.Queued, (await _service.GetAsync(OrgA, record.ImportId))!.Status);
        Assert.Equal(2, _imports.Queued.Count);
    }

    [Fact]
    public async Task An_import_is_invisible_to_another_organisation()
    {
        var record = await _service.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "u");

        Assert.Empty(await _service.ListAsync(OrgB));
        Assert.Null(await _service.GetAsync(OrgB, record.ImportId));
        Assert.Null(await _service.GetDocumentAsync(OrgB, record.ImportId));
        Assert.False(await _service.DismissAsync(OrgB, record.ImportId));
        Assert.False(await _service.RetryAsync(OrgB, record.ImportId));

        // Processing with the wrong organisation finds nothing to do.
        await _service.ProcessAsync(OrgB, record.ImportId);
        Assert.Equal(ImportStatus.Queued, (await _service.GetAsync(OrgA, record.ImportId))!.Status);
    }

    [Fact]
    public async Task Dismissing_takes_it_off_the_list_and_keeps_the_pdf()
    {
        var record = await _service.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "u");

        Assert.True(await _service.DismissAsync(OrgA, record.ImportId));

        Assert.Empty(await _service.ListAsync(OrgA));
        Assert.NotNull(await _documents.GetAsync(record.DocumentKey));
    }

    [Fact]
    public async Task A_mailed_pdf_waits_for_approval_and_is_only_read_after_it()
    {
        var record = await _service.SubmitAsync(OrgA, ImportSource.Email, "mail.pdf", Pdf, sender: "x@y.z");

        Assert.Equal(ImportStatus.AwaitingApproval, record.Status);
        Assert.Empty(_imports.Queued);

        // Even a stray queue message does not get it read.
        await _service.ProcessAsync(OrgA, record.ImportId);
        Assert.Equal(ImportStatus.AwaitingApproval, (await _service.GetAsync(OrgA, record.ImportId))!.Status);
        Assert.False(await _service.RetryAsync(OrgA, record.ImportId));

        Assert.False(await _service.ApproveAsync(OrgB, record.ImportId));
        Assert.True(await _service.ApproveAsync(OrgA, record.ImportId));
        Assert.Equal((OrgA, record.ImportId), Assert.Single(_imports.Queued));

        await _service.ProcessAsync(OrgA, record.ImportId);
        Assert.Equal(ImportStatus.Ready, (await _service.GetAsync(OrgA, record.ImportId))!.Status);
    }

    [Fact]
    public async Task Only_a_waiting_import_can_be_approved()
    {
        var upload = await _service.SubmitAsync(OrgA, ImportSource.Upload, "f.pdf", Pdf, "u");

        Assert.False(await _service.ApproveAsync(OrgA, upload.ImportId));
    }

    private sealed class Interpreter : IInvoiceInterpreter
    {
        public bool Fail { get; set; }

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => Fail
                ? throw new InvalidOperationException("The document is unreadable.")
                : Task.FromResult(new InvoiceInterpretationResult(
                    CompleteInvoice.Create("F-2026-001"), new ProviderExecution("test", "m", 0)));
    }

    private sealed class FakeImports : IImportRepository, IImportQueue
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
