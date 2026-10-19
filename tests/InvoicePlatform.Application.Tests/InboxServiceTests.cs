using Microsoft.Extensions.Logging.Abstractions;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class InboxServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";
    private const string Domain = "inbox.example.test";
    private const string PreviousDomain = "inbox.old.test";

    private static readonly byte[] Pdf = "%PDF-1.4 x"u8.ToArray();

    private readonly Directory _directory = new();
    private readonly Mailbox _mail = new();
    private readonly Imports _imports = new();
    private readonly InboxService _inbox;

    public InboxServiceTests()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-10-19T09:00:00Z"));
        var relations = new RelationService(new FakeRelationStore(), clock, new InvoiceRulesetRegistry());
        var extraction = new ExtractInvoiceService(
            new NeverCalled(), new InvoiceEnrichment(new FakeSettingsStore(), relations), new InvoiceCalculationValidator(),
            new CanonicalInvoiceXmlSerializer(), new LearningService(new FakeLearningStore(), clock), new FakePlatformStore(), new FakeUsageMeter(), new FakeUsageLedger(), clock, NullLogger<ExtractInvoiceService>.Instance);
        var imports = new ImportService(_imports, _imports, new FakeInvoiceStore(), extraction, clock);
        _inbox = new InboxService(_directory, _mail, imports, new InboxSettings(Domain, [PreviousDomain]));
    }

    [Fact]
    public async Task An_organisation_gets_a_hard_to_guess_address_on_the_inbox_domain()
    {
        var address = await _inbox.CreateAddressAsync(OrgA);

        Assert.Matches($"^[a-z2-9]{{12}}@{Domain.Replace(".", "\\.", StringComparison.Ordinal)}$", address);
        Assert.Equal(address, await _inbox.GetAddressAsync(OrgA));
    }

    [Fact]
    public async Task A_new_address_retires_the_old_one()
    {
        var first = await _inbox.CreateAddressAsync(OrgA);
        var second = await _inbox.CreateAddressAsync(OrgA);

        Assert.NotEqual(first, second);
        Assert.Null(await _directory.FindOrganizationAsync(first.Split('@')[0]));
        Assert.Equal(OrgA, await _directory.FindOrganizationAsync(second.Split('@')[0]));
    }

    [Fact]
    public async Task Every_pdf_attachment_becomes_an_import_with_sender_and_subject()
    {
        var address = await _inbox.CreateAddressAsync(OrgA);
        _mail.Messages["m1"] = new ReceivedMail("boekhouding@klant.nl", "Facturen oktober",
            [new MailAttachment("a.pdf", Pdf), new MailAttachment("b.pdf", Pdf), new MailAttachment("logo.pdf", "GIF89a"u8.ToArray())]);

        var created = await _inbox.ReceiveAsync("m1", [address]);

        Assert.Equal(2, created);
        Assert.All(_imports.Saved.Values, r =>
        {
            Assert.Equal((OrgA, ImportSource.Email, "boekhouding@klant.nl", "Facturen oktober"), (r.OrganizationId, r.Source, r.Sender, r.Subject));
        });
        // Waiting for a user's approval: nothing is read, so nothing is spent, until then.
        Assert.All(_imports.Saved.Values, r => Assert.Equal(ImportStatus.AwaitingApproval, r.Status));
        Assert.Empty(_imports.Queued);
    }

    [Fact]
    public async Task Mail_to_an_unknown_or_foreign_address_is_ignored_unread()
    {
        await _inbox.CreateAddressAsync(OrgA);
        _mail.Messages["m2"] = new ReceivedMail("x@y.z", "s", [new MailAttachment("a.pdf", Pdf)]);

        var created = await _inbox.ReceiveAsync("m2", [$"unknown@{Domain}", "someone@greenitsolutions.net"]);

        Assert.Equal(0, created);
        Assert.Equal(0, _mail.Reads);
    }

    [Fact]
    public async Task An_address_keeps_receiving_on_the_domain_the_inbox_moved_from()
    {
        var address = await _inbox.CreateAddressAsync(OrgA);
        var code = address.Split('@')[0];
        _mail.Messages["m3"] = new ReceivedMail("x@y.z", "s", [new MailAttachment("a.pdf", Pdf)]);

        var created = await _inbox.ReceiveAsync("m3", [$"{code}@{PreviousDomain.ToUpperInvariant()}"]);

        Assert.EndsWith($"@{Domain}", address, StringComparison.Ordinal);
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task Each_addressed_organisation_gets_its_own_copy()
    {
        var a = await _inbox.CreateAddressAsync(OrgA);
        var b = await _inbox.CreateAddressAsync(OrgB);
        _mail.Messages["m3"] = new ReceivedMail("x@y.z", "s", [new MailAttachment("a.pdf", Pdf)]);

        await _inbox.ReceiveAsync("m3", [a, b.ToUpperInvariant()]);

        Assert.Equal([OrgA, OrgB], _imports.Saved.Values.Select(r => r.OrganizationId).Order());
    }

    private sealed class Directory : IInboxDirectory
    {
        private readonly Dictionary<string, string> _byLocal = [];
        private readonly Dictionary<string, string> _byOrg = [];

        public Task<string?> FindOrganizationAsync(string localPart, CancellationToken cancellationToken = default)
            => Task.FromResult(_byLocal.GetValueOrDefault(localPart));

        public Task<string?> GetLocalPartAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_byOrg.GetValueOrDefault(organizationId));

        public Task AssignAsync(string organizationId, string localPart, string? previousLocalPart, CancellationToken cancellationToken = default)
        {
            _byLocal[localPart] = organizationId;
            _byOrg[organizationId] = localPart;
            if (previousLocalPart is not null)
            {
                _byLocal.Remove(previousLocalPart);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class Mailbox : IReceivedMailSource
    {
        public Dictionary<string, ReceivedMail> Messages { get; } = [];

        public int Reads { get; private set; }

        public Task<ReceivedMail?> ReadAsync(string messageId, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(Messages.GetValueOrDefault(messageId));
        }
    }

    private sealed class Imports : IImportRepository, IImportQueue
    {
        public Dictionary<string, ImportRecord> Saved { get; } = [];

        public List<string> Queued { get; } = [];

        public Task SaveAsync(ImportRecord record, CancellationToken cancellationToken = default)
        {
            Saved[record.ImportId] = record;
            return Task.CompletedTask;
        }

        public Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
            => Task.FromResult(Saved.GetValueOrDefault(importId) is { } r && r.OrganizationId == organizationId ? r : null);

        public Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImportRecord>>(Saved.Values.Where(r => r.OrganizationId == organizationId).ToList());

        public Task DeleteAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        {
            Saved.Remove(importId);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        {
            Queued.Add(importId);
            return Task.CompletedTask;
        }
    }

    private sealed class NeverCalled : IInvoiceInterpreter
    {
        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Receiving mail only queues; the worker reads later.");
    }
}
