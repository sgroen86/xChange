# Invoice Storage ("boeken") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A reviewed invoice can be booked, which stores its canonical data and the original PDF in xChange, and booked invoices are listed and reopenable.

**Architecture:** Domain gains `StoredInvoice`. Application declares `IInvoiceRepository` and `IDocumentStore` and orchestrates booking. Infrastructure implements them against DynamoDB and S3. Records are keyed on `ORG#<organizationId>`, so another tenant's invoice is unreachable rather than merely unauthorised. The API exposes book/list/get/document behind the existing login.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, AWS SDK (DynamoDB, S3), xUnit, Next.js 16 static export.

**Out of scope for this plan:** PEPPOL export. That is a separate plan against the same spec.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/InvoicePlatform.Domain/Invoices/StoredInvoice.cs` | The stored record and its status |
| `src/InvoicePlatform.Application/Invoices/IInvoiceStores.cs` | `IInvoiceRepository`, `IDocumentStore` |
| `src/InvoicePlatform.Application/Invoices/BookInvoiceService.cs` | The booking use case |
| `src/InvoicePlatform.Infrastructure/Invoices/DynamoDbInvoiceRepository.cs` | DynamoDB persistence |
| `src/InvoicePlatform.Infrastructure/Invoices/S3DocumentStore.cs` | PDF storage |
| `src/InvoicePlatform.Contracts/Invoices/StoredInvoiceResponse.cs` | Wire shapes |
| `src/InvoicePlatform.Api/Invoices/StoredInvoiceEndpoints.cs` | HTTP surface |
| `infra/aws/service.yml` | Bucket, table, IAM |
| `apps/web/lib/invoices.ts` | Frontend client |
| `apps/web/app/invoices/page.tsx` + `InvoiceListClient.tsx` | The list screen |
| `tests/InvoicePlatform.Application.Tests/BookInvoiceServiceTests.cs` | Booking and tenant isolation |
| `tests/InvoicePlatform.Integration.Tests/StoredInvoiceEndpointTests.cs` | Auth and role enforcement |

---

## Task 1: Remove the UI text that is no longer true

The upload screen still says processing is simulated and that no language model or AWS service is called. Both became false when real extraction shipped.

**Files:**
- Modify: `apps/web/app/upload/page.tsx`

- [ ] **Step 1: Remove the "Wat er nog niet gebeurt" card**

Delete this entire block from `apps/web/app/upload/page.tsx`:

```tsx
          <div className="card">
            <div className="card__header">
              <h3>Wat er nog niet gebeurt</h3>
            </div>
            <div className="card__body">
              <div className="alert alert--info">
                <Icon name="info" />
                <div>
                  Er wordt geen OCR, taalmodel, AWS-dienst of PEPPOL-verzending aangeroepen. De
                  gegevens op het beoordelingsscherm zijn mockdata.
                </div>
              </div>
            </div>
          </div>
```

- [ ] **Step 2: Correct the page subtitle**

Replace:

```tsx
            Upload een PDF-factuur. De verwerking is in deze fase gesimuleerd.
```

with:

```tsx
            Upload een PDF-factuur. De gegevens worden uitgelezen en daarna ter controle
            voorgelegd.
```

- [ ] **Step 3: Verify the build**

Run: `cd apps/web && npm run lint && npm run build`
Expected: lint exits 0, build exits 0.

- [ ] **Step 4: Commit**

```bash
git add apps/web/app/upload/page.tsx
git commit -m "Remove upload-screen text that is no longer true"
```

---

## Task 2: The stored invoice record

**Files:**
- Create: `src/InvoicePlatform.Domain/Invoices/StoredInvoice.cs`

- [ ] **Step 1: Write the record**

```csharp
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Invoices;

/// <summary>Where an invoice is in its short life.</summary>
public enum InvoiceStatus
{
    /// <summary>Extracted and reviewable, not yet committed to.</summary>
    Draft,

    /// <summary>The user has booked it: kept deliberately, not incidentally.</summary>
    Booked,
}

/// <summary>
/// An invoice as it is kept.
///
/// <see cref="OrganizationId"/> is the tenant key required by CLAUDE.md, and it
/// is also the partition key in storage: an invoice belonging to another
/// organisation is not merely unauthorised, it is in a different partition.
///
/// The PDF is not here. <see cref="DocumentKey"/> points at object storage,
/// because a database is the wrong place for blobs.
/// </summary>
public sealed record StoredInvoice
{
    public required string InvoiceId { get; init; }

    public required string OrganizationId { get; init; }

    public required InvoiceStatus Status { get; init; }

    public required CanonicalInvoiceDraft Invoice { get; init; }

    /// <summary>Object-storage key of the source PDF. Null until one is stored.</summary>
    public string? DocumentKey { get; init; }

    public string? SourceFileName { get; init; }

    public long SourceByteSize { get; init; }

    /// <summary>Model that produced the extraction, for provenance.</summary>
    public string? ProviderModel { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string CreatedByUserId { get; init; }

    public DateTimeOffset? BookedAt { get; init; }

    public string? BookedByUserId { get; init; }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/InvoicePlatform.Domain/Invoices/StoredInvoice.cs
git commit -m "Add the stored invoice record"
```

---

## Task 3: Store interfaces and the booking use case

**Files:**
- Create: `src/InvoicePlatform.Application/Invoices/IInvoiceStores.cs`
- Create: `src/InvoicePlatform.Application/Invoices/BookInvoiceService.cs`
- Test: `tests/InvoicePlatform.Application.Tests/BookInvoiceServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/InvoicePlatform.Application.Tests/BookInvoiceServiceTests.cs`:

```csharp
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Tests;

public class BookInvoiceServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeInvoiceStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-09-08T12:00:00Z"));
    private readonly BookInvoiceService _service;

    public BookInvoiceServiceTests()
        => _service = new BookInvoiceService(_store, _store, _clock);

    private static CanonicalInvoiceDraft ADraft(string number = "F-1") => new()
    {
        InvoiceNumber = number,
        CurrencyCode = "EUR",
        Lines = [new CanonicalInvoiceLine { Description = "Werk", NetAmount = 100m }],
    };

    [Fact]
    public async Task Booking_stores_the_invoice_and_the_document()
    {
        var stored = await _service.BookAsync(
            OrgA, "user-1", ADraft(), new byte[] { 1, 2, 3 }, "factuur.pdf", "claude-opus-5");

        Assert.Equal(InvoiceStatus.Booked, stored.Status);
        Assert.Equal(OrgA, stored.OrganizationId);
        Assert.Equal("user-1", stored.BookedByUserId);
        Assert.Equal(_clock.UtcNow, stored.BookedAt);
        Assert.NotNull(stored.DocumentKey);
        Assert.Equal(3, _store.Documents[stored.DocumentKey!].Length);
    }

    [Fact]
    public async Task An_invoice_is_invisible_to_another_organisation()
    {
        // The property that matters most: a tenant cannot reach another
        // tenant's invoice even knowing its id.
        var stored = await _service.BookAsync(
            OrgA, "user-1", ADraft(), new byte[] { 1 }, "a.pdf", "claude-opus-5");

        Assert.NotNull(await _service.GetAsync(OrgA, stored.InvoiceId));
        Assert.Null(await _service.GetAsync(OrgB, stored.InvoiceId));
    }

    [Fact]
    public async Task Listing_returns_only_the_callers_organisation()
    {
        await _service.BookAsync(OrgA, "u", ADraft("A-1"), new byte[] { 1 }, "a.pdf", "m");
        await _service.BookAsync(OrgA, "u", ADraft("A-2"), new byte[] { 1 }, "a.pdf", "m");
        await _service.BookAsync(OrgB, "u", ADraft("B-1"), new byte[] { 1 }, "b.pdf", "m");

        var forA = await _service.ListAsync(OrgA);

        Assert.Equal(2, forA.Count);
        Assert.DoesNotContain(forA, i => i.Invoice.InvoiceNumber == "B-1");
    }

    [Fact]
    public async Task Booking_the_same_invoice_again_replaces_it_rather_than_duplicating()
    {
        var first = await _service.BookAsync(
            OrgA, "u", ADraft("F-1"), new byte[] { 1 }, "a.pdf", "m");

        var again = await _service.BookAsync(
            OrgA, "u", ADraft("F-1") with { Note = "gecorrigeerd" },
            new byte[] { 1 }, "a.pdf", "m", first.InvoiceId);

        Assert.Equal(first.InvoiceId, again.InvoiceId);
        Assert.Single(await _service.ListAsync(OrgA));
        Assert.Equal("gecorrigeerd", (await _service.GetAsync(OrgA, first.InvoiceId))!.Invoice.Note);
    }

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeInvoiceStore : IInvoiceRepository, IDocumentStore
    {
        private readonly Dictionary<(string Org, string Id), StoredInvoice> _invoices = [];

        public Dictionary<string, byte[]> Documents { get; } = [];

        public Task SaveAsync(StoredInvoice invoice, CancellationToken cancellationToken = default)
        {
            _invoices[(invoice.OrganizationId, invoice.InvoiceId)] = invoice;
            return Task.CompletedTask;
        }

        public Task<StoredInvoice?> GetAsync(string organizationId, string invoiceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_invoices.GetValueOrDefault((organizationId, invoiceId)));

        public Task<IReadOnlyList<StoredInvoice>> ListAsync(string organizationId, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredInvoice>>(
                _invoices.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());

        public Task<string> PutAsync(string organizationId, string invoiceId, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
        {
            var key = $"{organizationId}/{invoiceId}.pdf";
            Documents[key] = content.ToArray();
            return Task.FromResult(key);
        }

        public Task<byte[]?> GetAsync(string documentKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.GetValueOrDefault(documentKey));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/InvoicePlatform.Application.Tests --nologo`
Expected: FAIL — `IInvoiceRepository`, `IDocumentStore` and `BookInvoiceService` do not exist.

- [ ] **Step 3: Write the interfaces**

Create `src/InvoicePlatform.Application/Invoices/IInvoiceStores.cs`:

```csharp
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Persistence for invoices. Every method takes the organisation explicitly:
/// there is no ambient tenant, so a caller cannot forget to scope a query
/// (CLAUDE.md, hard rule 4).
/// </summary>
public interface IInvoiceRepository
{
    Task SaveAsync(StoredInvoice invoice, CancellationToken cancellationToken = default);

    Task<StoredInvoice?> GetAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredInvoice>> ListAsync(
        string organizationId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>Object storage for the source documents.</summary>
public interface IDocumentStore
{
    /// <returns>The key under which the document was stored.</returns>
    Task<string> PutAsync(
        string organizationId,
        string invoiceId,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetAsync(string documentKey, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Write the use case**

Create `src/InvoicePlatform.Application/Invoices/BookInvoiceService.cs`:

```csharp
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Booking an invoice: keeping it deliberately rather than incidentally.
///
/// The document is stored before the record, so a record never points at a key
/// that does not exist. The reverse order would leave a listed invoice whose
/// PDF cannot be fetched.
/// </summary>
public sealed class BookInvoiceService(
    IInvoiceRepository invoices,
    IDocumentStore documents,
    IClock clock)
{
    /// <param name="invoiceId">
    /// Supply an existing id to re-book a correction; omit it for a new invoice.
    /// </param>
    public async Task<StoredInvoice> BookAsync(
        string organizationId,
        string userId,
        CanonicalInvoiceDraft draft,
        ReadOnlyMemory<byte> pdf,
        string sourceFileName,
        string providerModel,
        string? invoiceId = null,
        CancellationToken cancellationToken = default)
    {
        var id = invoiceId ?? Guid.NewGuid().ToString("n");
        var now = clock.UtcNow;

        var documentKey = await documents
            .PutAsync(organizationId, id, pdf, "application/pdf", cancellationToken)
            .ConfigureAwait(false);

        var stored = new StoredInvoice
        {
            InvoiceId = id,
            OrganizationId = organizationId,
            Status = InvoiceStatus.Booked,
            Invoice = draft,
            DocumentKey = documentKey,
            SourceFileName = sourceFileName,
            SourceByteSize = pdf.Length,
            ProviderModel = providerModel,
            CreatedAt = now,
            CreatedByUserId = userId,
            BookedAt = now,
            BookedByUserId = userId,
        };

        await invoices.SaveAsync(stored, cancellationToken).ConfigureAwait(false);
        return stored;
    }

    public Task<StoredInvoice?> GetAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default)
        => invoices.GetAsync(organizationId, invoiceId, cancellationToken);

    public Task<IReadOnlyList<StoredInvoice>> ListAsync(
        string organizationId,
        CancellationToken cancellationToken = default)
        => invoices.ListAsync(organizationId, cancellationToken: cancellationToken);

    public async Task<byte[]?> GetDocumentAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        // Resolved through the invoice, so a document key from another tenant
        // cannot be fetched by guessing it.
        var invoice = await invoices.GetAsync(organizationId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return invoice?.DocumentKey is null
            ? null
            : await documents.GetAsync(invoice.DocumentKey, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/InvoicePlatform.Application.Tests --nologo`
Expected: PASS, 22 tests (18 existing + 4 new).

- [ ] **Step 6: Commit**

```bash
git add src/InvoicePlatform.Application/Invoices/ tests/InvoicePlatform.Application.Tests/BookInvoiceServiceTests.cs
git commit -m "Add the booking use case, scoped by organisation"
```

---

## Task 4: DynamoDB repository

**Files:**
- Create: `src/InvoicePlatform.Infrastructure/Invoices/DynamoDbInvoiceRepository.cs`

- [ ] **Step 1: Write the repository**

```csharp
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Invoices;

/// <summary>
/// Invoices in DynamoDB, partitioned by organisation.
///
///   pk = ORG#&lt;organizationId&gt;
///   sk = INVOICE#&lt;invoiceId&gt;
///
/// The tenant is the partition key rather than a filter, so a query for one
/// organisation cannot return another's rows even if the code forgot to check.
/// Listing is a Query on the partition, never a Scan.
/// </summary>
internal sealed class DynamoDbInvoiceRepository(
    IAmazonDynamoDB dynamo,
    IOptions<InvoiceStorageOptions> options)
    : IInvoiceRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string _table = options.Value.TableName;

    public Task SaveAsync(StoredInvoice invoice, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{invoice.OrganizationId}"),
                    ["sk"] = new($"INVOICE#{invoice.InvoiceId}"),
                    ["invoiceId"] = new(invoice.InvoiceId),
                    ["organizationId"] = new(invoice.OrganizationId),
                    ["status"] = new(invoice.Status.ToString()),
                    ["invoice"] = new(JsonSerializer.Serialize(invoice.Invoice, Json)),
                    ["documentKey"] = Nullable(invoice.DocumentKey),
                    ["sourceFileName"] = Nullable(invoice.SourceFileName),
                    ["sourceByteSize"] = new AttributeValue { N = invoice.SourceByteSize.ToString() },
                    ["providerModel"] = Nullable(invoice.ProviderModel),
                    ["createdAt"] = new(invoice.CreatedAt.ToString("O")),
                    ["createdByUserId"] = new(invoice.CreatedByUserId),
                    ["bookedAt"] = Nullable(invoice.BookedAt?.ToString("O")),
                    ["bookedByUserId"] = Nullable(invoice.BookedByUserId),
                },
            },
            cancellationToken);

    public async Task<StoredInvoice?> GetAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{organizationId}"),
                    ["sk"] = new($"INVOICE#{invoiceId}"),
                },
            },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet ? ToInvoice(response.Item) : null;
    }

    public async Task<IReadOnlyList<StoredInvoice>> ListAsync(
        string organizationId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await dynamo.QueryAsync(
            new QueryRequest
            {
                TableName = _table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new($"ORG#{organizationId}"),
                    [":sk"] = new("INVOICE#"),
                },
                Limit = limit,
            },
            cancellationToken).ConfigureAwait(false);

        return response.Items.Select(ToInvoice).ToList();
    }

    private static AttributeValue Nullable(string? value)
        => value is null ? new AttributeValue { NULL = true } : new AttributeValue(value);

    private static StoredInvoice ToInvoice(Dictionary<string, AttributeValue> item) => new()
    {
        InvoiceId = item["invoiceId"].S,
        OrganizationId = item["organizationId"].S,
        Status = Enum.TryParse<InvoiceStatus>(item["status"].S, out var status)
            ? status
            : InvoiceStatus.Draft,
        Invoice = JsonSerializer.Deserialize<CanonicalInvoiceDraft>(item["invoice"].S, Json)
            ?? new CanonicalInvoiceDraft(),
        DocumentKey = item["documentKey"].S,
        SourceFileName = item["sourceFileName"].S,
        SourceByteSize = long.TryParse(item["sourceByteSize"].N, out var size) ? size : 0,
        ProviderModel = item["providerModel"].S,
        CreatedAt = DateTimeOffset.Parse(item["createdAt"].S),
        CreatedByUserId = item["createdByUserId"].S,
        BookedAt = item["bookedAt"].S is { } booked ? DateTimeOffset.Parse(booked) : null,
        BookedByUserId = item["bookedByUserId"].S,
    };
}

public sealed class InvoiceStorageOptions
{
    public const string SectionName = "InvoiceStorage";

    public string TableName { get; set; } = "xchange-invoices";

    public string BucketName { get; set; } = "";
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/InvoicePlatform.Infrastructure/Invoices/DynamoDbInvoiceRepository.cs
git commit -m "Store invoices in DynamoDB, partitioned by organisation"
```

---

## Task 5: S3 document store

**Files:**
- Create: `src/InvoicePlatform.Infrastructure/Invoices/S3DocumentStore.cs`
- Modify: `src/InvoicePlatform.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Add the S3 package**

Run: `dotnet add src/InvoicePlatform.Infrastructure package AWSSDK.S3`
Expected: PackageReference added.

- [ ] **Step 2: Write the store**

Create `src/InvoicePlatform.Infrastructure/Invoices/S3DocumentStore.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using InvoicePlatform.Application.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Invoices;

/// <summary>
/// Source PDFs in S3.
///
/// The key starts with the organisation id, so an object listing is already
/// segmented by tenant. Callers never pass a key in from outside: it is read
/// back off the invoice record, which is itself scoped by organisation.
/// </summary>
internal sealed class S3DocumentStore(
    IAmazonS3 s3,
    IOptions<InvoiceStorageOptions> options)
    : IDocumentStore
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task<string> PutAsync(
        string organizationId,
        string invoiceId,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = $"{organizationId}/{invoiceId}.pdf";

        using var stream = new MemoryStream(content.ToArray(), writable: false);

        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
            },
            cancellationToken).ConfigureAwait(false);

        return key;
    }

    public async Task<byte[]?> GetAsync(
        string documentKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await s3
                .GetObjectAsync(_bucket, documentKey, cancellationToken)
                .ConfigureAwait(false);

            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // A missing document is an absent value, not an error for callers.
            return null;
        }
    }
}
```

- [ ] **Step 3: Register everything**

In `src/InvoicePlatform.Infrastructure/DependencyInjection.cs`, add these usings at the top:

```csharp
using Amazon.S3;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Infrastructure.Invoices;
```

and add this method after `AddIdentity`:

```csharp
    /// <summary>Invoice persistence: records in DynamoDB, documents in S3.</summary>
    public static IServiceCollection AddInvoiceStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<InvoiceStorageOptions>()
            .Bind(configuration.GetSection(InvoiceStorageOptions.SectionName));

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
        services.AddSingleton<IInvoiceRepository, DynamoDbInvoiceRepository>();
        services.AddSingleton<IDocumentStore, S3DocumentStore>();
        services.AddScoped<BookInvoiceService>();

        return services;
    }
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/InvoicePlatform.Infrastructure/
git commit -m "Store source PDFs in S3"
```

---

## Task 6: API endpoints

**Files:**
- Create: `src/InvoicePlatform.Contracts/Invoices/StoredInvoiceResponse.cs`
- Create: `src/InvoicePlatform.Api/Invoices/StoredInvoiceEndpoints.cs`
- Modify: `src/InvoicePlatform.Api/Program.cs`
- Test: `tests/InvoicePlatform.Integration.Tests/StoredInvoiceEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/InvoicePlatform.Integration.Tests/StoredInvoiceEndpointTests.cs`:

```csharp
using System.Net;
using System.Text;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The stored-invoice routes must be behind the login. An unauthenticated
/// listing would expose every tenant's invoices at once, so this is asserted
/// rather than assumed.
/// </summary>
public class StoredInvoiceEndpointTests : IClassFixture<RouteProtectionTests.Factory>
{
    private readonly RouteProtectionTests.Factory _factory;

    public StoredInvoiceEndpointTests(RouteProtectionTests.Factory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/v1/invoices")]
    [InlineData("/api/v1/invoices/some-id")]
    [InlineData("/api/v1/invoices/some-id/document")]
    public async Task Reading_invoices_requires_a_login(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Booking_requires_a_login()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/invoices",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/InvoicePlatform.Integration.Tests --nologo`
Expected: FAIL — the routes return 404, not 401, because they do not exist yet.

- [ ] **Step 3: Write the contracts**

Create `src/InvoicePlatform.Contracts/Invoices/StoredInvoiceResponse.cs`:

```csharp
using System.Text.Json.Serialization;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Contracts.Invoices;

public sealed record BookInvoiceRequest
{
    /// <summary>Supply to re-book a correction; omit for a new invoice.</summary>
    [JsonPropertyName("invoiceId")]
    public string? InvoiceId { get; init; }

    [JsonPropertyName("canonicalInvoice")]
    public required CanonicalInvoiceDraft CanonicalInvoice { get; init; }

    /// <summary>Base64 of the source PDF.</summary>
    [JsonPropertyName("documentBase64")]
    public required string DocumentBase64 { get; init; }

    [JsonPropertyName("sourceFileName")]
    public required string SourceFileName { get; init; }

    [JsonPropertyName("providerModel")]
    public string? ProviderModel { get; init; }
}

public sealed record StoredInvoiceResponse
{
    [JsonPropertyName("invoiceId")]
    public required string InvoiceId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("canonicalInvoice")]
    public required CanonicalInvoiceDraft CanonicalInvoice { get; init; }

    [JsonPropertyName("sourceFileName")]
    public string? SourceFileName { get; init; }

    [JsonPropertyName("providerModel")]
    public string? ProviderModel { get; init; }

    [JsonPropertyName("bookedAt")]
    public DateTimeOffset? BookedAt { get; init; }
}
```

- [ ] **Step 4: Write the endpoints**

Create `src/InvoicePlatform.Api/Invoices/StoredInvoiceEndpoints.cs`:

```csharp
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Contracts.Invoices;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Api.Invoices;

/// <summary>
/// HTTP surface for stored invoices. The organisation comes from the signed-in
/// user and never from the request, so a caller cannot ask for someone else's
/// invoices by changing a parameter.
/// </summary>
public static class StoredInvoiceEndpoints
{
    public static RouteGroupBuilder MapStoredInvoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/invoices", BookAsync)
            .WithName("BookInvoice")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapGet("/invoices", ListAsync)
            .WithName("ListInvoices")
            .RequireAuthenticatedUser();

        group.MapGet("/invoices/{invoiceId}", GetAsync)
            .WithName("GetInvoice")
            .RequireAuthenticatedUser();

        group.MapGet("/invoices/{invoiceId}/document", GetDocumentAsync)
            .WithName("GetInvoiceDocument")
            .RequireAuthenticatedUser();

        return group;
    }

    private static async Task<IResult> BookAsync(
        BookInvoiceRequest request,
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        byte[] pdf;
        try
        {
            pdf = Convert.FromBase64String(request.DocumentBase64);
        }
        catch (FormatException)
        {
            return Results.Problem(
                detail: "Het document is geen geldige base64.",
                title: "Ongeldig document",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var stored = await service.BookAsync(
            user.OrganizationId,
            user.Id,
            request.CanonicalInvoice,
            pdf,
            request.SourceFileName,
            request.ProviderModel ?? "onbekend",
            request.InvoiceId,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(ToResponse(stored));
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var invoices = await service.ListAsync(user.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(invoices.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        string invoiceId,
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var invoice = await service.GetAsync(user.OrganizationId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return invoice is null ? Results.NotFound() : Results.Ok(ToResponse(invoice));
    }

    private static async Task<IResult> GetDocumentAsync(
        string invoiceId,
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var pdf = await service.GetDocumentAsync(user.OrganizationId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return pdf is null ? Results.NotFound() : Results.File(pdf, "application/pdf");
    }

    private static StoredInvoiceResponse ToResponse(StoredInvoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        Status = invoice.Status.ToString(),
        CanonicalInvoice = invoice.Invoice,
        SourceFileName = invoice.SourceFileName,
        ProviderModel = invoice.ProviderModel,
        BookedAt = invoice.BookedAt,
    };
}
```

- [ ] **Step 5: Register the endpoints and the services**

In `src/InvoicePlatform.Api/Program.cs`, after this line:

```csharp
builder.Services.AddIdentity(builder.Configuration);
```

add:

```csharp
builder.Services.AddInvoiceStorage(builder.Configuration);
```

and after this line:

```csharp
api.MapInvoiceEndpoints();
```

add:

```csharp
api.MapStoredInvoiceEndpoints();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --nologo`
Expected: PASS, all suites. The four new endpoint tests return 401.

- [ ] **Step 7: Commit**

```bash
git add src/InvoicePlatform.Contracts/ src/InvoicePlatform.Api/ tests/InvoicePlatform.Integration.Tests/
git commit -m "Add stored-invoice endpoints behind the login"
```

---

## Task 7: Infrastructure — bucket, table, permissions

**Files:**
- Modify: `infra/aws/service.yml`
- Modify: `infra/aws/bootstrap.yml`

- [ ] **Step 1: Add the table and bucket to the service stack**

In `infra/aws/service.yml`, add after the `IdentityTable` resource:

```yaml
  # Booked invoices. Same reasoning as the identity table: DynamoDB is free at
  # this volume where RDS is not, and the store interfaces make a later move to
  # PostgreSQL a one-class change.
  InvoiceTable:
    Type: AWS::DynamoDB::Table
    # Booked invoices are the user's own records, not build output.
    DeletionPolicy: Retain
    UpdateReplacePolicy: Retain
    Properties:
      TableName: xchange-invoices
      BillingMode: PAY_PER_REQUEST
      AttributeDefinitions:
        - AttributeName: pk
          AttributeType: S
        - AttributeName: sk
          AttributeType: S
      KeySchema:
        - AttributeName: pk
          KeyType: HASH
        - AttributeName: sk
          KeyType: RANGE
      SSESpecification:
        SSEEnabled: true
      Tags:
        - Key: Project
          Value: xchange

  # Source PDFs. Private and encrypted; re-booking an invoice overwrites its own
  # document rather than accumulating copies.
  DocumentBucket:
    Type: AWS::S3::Bucket
    DeletionPolicy: Retain
    UpdateReplacePolicy: Retain
    Properties:
      BucketName: !Sub xchange-documents-${AWS::AccountId}
      PublicAccessBlockConfiguration:
        BlockPublicAcls: true
        BlockPublicPolicy: true
        IgnorePublicAcls: true
        RestrictPublicBuckets: true
      BucketEncryption:
        ServerSideEncryptionConfiguration:
          - ServerSideEncryptionByDefault:
              SSEAlgorithm: AES256
      Tags:
        - Key: Project
          Value: xchange
```

- [ ] **Step 2: Give the function access**

In `infra/aws/service.yml`, inside `FunctionRole` → `Policies`, add after the `identity-table` policy:

```yaml
        - PolicyName: invoice-storage
          PolicyDocument:
            Version: '2012-10-17'
            Statement:
              - Effect: Allow
                Action:
                  - dynamodb:GetItem
                  - dynamodb:PutItem
                  - dynamodb:Query
                Resource: !GetAtt InvoiceTable.Arn
              - Effect: Allow
                Action:
                  - s3:GetObject
                  - s3:PutObject
                Resource: !Sub ${DocumentBucket.Arn}/*
```

- [ ] **Step 3: Pass the names to the function**

In `infra/aws/service.yml`, inside `Function` → `Environment` → `Variables`, add:

```yaml
          InvoiceStorage__TableName: !Ref InvoiceTable
          InvoiceStorage__BucketName: !Ref DocumentBucket
```

- [ ] **Step 4: Let CI create the bucket**

In `infra/aws/bootstrap.yml`, add a new statement after the `IdentityTable` statement:

```yaml
              - Sid: DocumentBucket
                Effect: Allow
                Action:
                  - s3:CreateBucket
                  - s3:DeleteBucket
                  - s3:GetBucketLocation
                  - s3:GetBucketPolicy
                  - s3:GetBucketPublicAccessBlock
                  - s3:GetBucketTagging
                  - s3:GetEncryptionConfiguration
                  - s3:PutBucketPolicy
                  - s3:PutBucketPublicAccessBlock
                  - s3:PutBucketTagging
                  - s3:PutEncryptionConfiguration
                Resource: !Sub arn:aws:s3:::xchange-documents-${AWS::AccountId}
```

The existing `IdentityTable` statement already uses an `arn:aws:dynamodb:...:table/xchange-*` wildcard, so it covers the new table without change. Confirm that is still the case.

- [ ] **Step 5: Validate both templates**

Run:

```bash
python -c "import yaml; L=type('L',(yaml.SafeLoader,),{}); L.add_multi_constructor('!', lambda l,s,n: None); [print(f, 'OK') for f in ['infra/aws/service.yml','infra/aws/bootstrap.yml'] if yaml.load(open(f,encoding='utf-8'),Loader=L)]"
```

Expected: both print OK.

- [ ] **Step 6: Redeploy the bootstrap stack**

The deploy role needs the S3 permissions before CI can create the bucket.

```bash
aws cloudformation deploy \
  --template-file infra/aws/bootstrap.yml \
  --stack-name xchange-bootstrap \
  --capabilities CAPABILITY_NAMED_IAM \
  --region eu-central-1 \
  --parameter-overrides CreateOidcProvider=true
```

Expected: "Successfully created/updated stack".

- [ ] **Step 7: Commit**

```bash
git add infra/aws/
git commit -m "Provision the invoice table and document bucket"
```

---

## Task 8: Frontend client

**Files:**
- Create: `apps/web/lib/invoices.ts`

- [ ] **Step 1: Write the client**

```typescript
/**
 * Client for stored invoices.
 *
 * The organisation is never sent: the API takes it from the session, so the
 * browser cannot ask for another tenant's data by changing a parameter.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";
import type { CanonicalInvoiceDraft } from "./canonical";

export interface StoredInvoice {
  invoiceId: string;
  status: "Draft" | "Booked";
  canonicalInvoice: Record<string, unknown>;
  sourceFileName: string | null;
  providerModel: string | null;
  bookedAt: string | null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  if (!isApiConfigured) {
    throw new ApiError("Er is geen API geconfigureerd.", 0);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: { ...(init?.headers ?? {}), ...authHeaders() },
    });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null;
    throw new ApiError(problem?.detail ?? `De API gaf status ${response.status}.`, response.status);
  }

  return (await response.json()) as T;
}

/** Base64 without the data: prefix, which the API does not expect. */
async function toBase64(blob: Blob): Promise<string> {
  const buffer = await blob.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let index = 0; index < bytes.length; index++) {
    binary += String.fromCharCode(bytes[index]);
  }
  return btoa(binary);
}

export async function bookInvoice(input: {
  invoiceId?: string;
  canonicalInvoice: CanonicalInvoiceDraft;
  pdf: Blob;
  sourceFileName: string;
  providerModel?: string;
}): Promise<StoredInvoice> {
  return request<StoredInvoice>("/api/v1/invoices", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      invoiceId: input.invoiceId,
      canonicalInvoice: input.canonicalInvoice,
      documentBase64: await toBase64(input.pdf),
      sourceFileName: input.sourceFileName,
      providerModel: input.providerModel,
    }),
  });
}

export function listInvoices(): Promise<StoredInvoice[]> {
  return request<StoredInvoice[]>("/api/v1/invoices");
}

export function getInvoice(invoiceId: string): Promise<StoredInvoice> {
  return request<StoredInvoice>(`/api/v1/invoices/${invoiceId}`);
}
```

- [ ] **Step 2: Verify lint and build**

Run: `cd apps/web && npm run lint && npm run build`
Expected: both exit 0.

- [ ] **Step 3: Commit**

```bash
git add apps/web/lib/invoices.ts
git commit -m "Add the stored-invoice API client"
```

---

## Task 9: Book button on the review screen

**Files:**
- Modify: `apps/web/app/invoices/[invoiceId]/review/ReviewClient.tsx`

- [ ] **Step 1: Add the import**

Add to the imports at the top of `ReviewClient.tsx`:

```tsx
import { bookInvoice } from "../../../../lib/invoices";
```

- [ ] **Step 2: Add the state and handler**

Add after the existing `onSave` handler:

```tsx
  const [booking, setBooking] = useState(false);

  const onBook = async () => {
    if (!draft || booking) return;

    if (pdf.status !== "ready") {
      showToast("De PDF is niet beschikbaar; upload hem opnieuw voordat je boekt.", "danger");
      return;
    }

    setBooking(true);
    try {
      const blob = await (await fetch(pdf.url)).blob();
      await bookInvoice({
        invoiceId,
        canonicalInvoice: draft,
        pdf: blob,
        sourceFileName: draft.extraction.sourceFileName,
        providerModel: draft.extraction.engine,
      });
      saveDraft(draft);
      showToast("Factuur geboekt en opgeslagen.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Boeken is mislukt.", "danger");
    } finally {
      setBooking(false);
    }
  };
```

- [ ] **Step 3: Add the button**

In the `page-header__actions` block, add before the download button:

```tsx
          <button
            type="button"
            className="btn btn--secondary"
            onClick={() => void onBook()}
            disabled={booking}
          >
            <Icon name="save" /> {booking ? "Bezig…" : "Boeken"}
          </button>
```

- [ ] **Step 4: Verify lint and build**

Run: `cd apps/web && npm run lint && npm run build`
Expected: both exit 0.

- [ ] **Step 5: Commit**

```bash
git add apps/web/app/invoices/
git commit -m "Add a book action to the review screen"
```

---

## Task 10: The invoice list screen

**Files:**
- Create: `apps/web/app/invoices/page.tsx`
- Create: `apps/web/app/invoices/InvoiceListClient.tsx`
- Modify: `apps/web/components/nav.ts`
- Modify: `apps/web/components/AppShell.tsx`

- [ ] **Step 1: Create the route**

`apps/web/app/invoices/page.tsx`:

```tsx
import InvoiceListClient from "./InvoiceListClient";

export default function InvoicesPage() {
  return <InvoiceListClient />;
}
```

- [ ] **Step 2: Create the list component**

`apps/web/app/invoices/InvoiceListClient.tsx`:

```tsx
"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { listInvoices, type StoredInvoice } from "../../lib/invoices";

type State =
  | { status: "loading" }
  | { status: "ready"; invoices: StoredInvoice[] }
  | { status: "error"; message: string };

export default function InvoiceListClient() {
  const [state, setState] = useState<State>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;

    listInvoices()
      .then((invoices) => {
        if (!cancelled) setState({ status: "ready", invoices });
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setState({
            status: "error",
            message: cause instanceof Error ? cause.message : "Laden is mislukt.",
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="fileText" /> Facturen
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready"
              ? `${state.invoices.length} geboekte factuur(en)`
              : "Laden…"}
          </p>
        </div>
        <div className="page-header__actions">
          <Link className="btn btn--primary" href="/upload">
            <Icon name="fileUp" /> Nieuwe factuur
          </Link>
        </div>
      </div>

      {state.status === "error" && (
        <div className="alert alert--danger">
          <Icon name="x" />
          <div>{state.message}</div>
        </div>
      )}

      {state.status === "ready" && state.invoices.length === 0 && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">
              Er zijn nog geen facturen geboekt. Upload er een en klik op Boeken.
            </p>
          </div>
        </div>
      )}

      {state.status === "ready" && state.invoices.length > 0 && (
        <div className="card">
          <div className="card__body card__body--flush">
            <div className="table-wrap">
              <table className="table table--clickable">
                <thead>
                  <tr>
                    <th>Nummer</th>
                    <th>Leverancier</th>
                    <th>Datum</th>
                    <th>Bestand</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {state.invoices.map((invoice) => {
                    const inv = invoice.canonicalInvoice as {
                      invoiceNumber?: string;
                      issueDate?: string;
                      seller?: { name?: string };
                    };
                    return (
                      <tr key={invoice.invoiceId}>
                        <td>
                          <Link href={`/invoices/${invoice.invoiceId}/review`}>
                            {inv.invoiceNumber ?? "zonder nummer"}
                          </Link>
                        </td>
                        <td>{inv.seller?.name ?? "—"}</td>
                        <td>{inv.issueDate ?? "—"}</td>
                        <td className="text-soft">{invoice.sourceFileName ?? "—"}</td>
                        <td>
                          <span className="badge badge--success">Geboekt</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
```

- [ ] **Step 3: Add the nav entry**

In `apps/web/components/nav.ts`, add after the `upload` entry in `navStructure`:

```typescript
  { id: "invoices", label: "Facturen", icon: "fileText", href: "/invoices" },
```

and add to `pageTitles`:

```typescript
  invoices: { title: "Facturen", breadcrumb: "xChange / Facturen" },
```

- [ ] **Step 4: Make the nav highlight the right entry**

In `apps/web/components/AppShell.tsx`, replace the whole `activeNavId` function with:

```tsx
function activeNavId(pathname: string): string {
  if (/\/invoices\/[^/]+\/review/.test(pathname)) return "review";
  if (pathname.includes("/invoices")) return "invoices";
  return "upload";
}
```

- [ ] **Step 5: Verify lint and build**

Run: `cd apps/web && npm run lint && npm run build`
Expected: both exit 0, and the printed route list includes `/invoices`.

- [ ] **Step 6: Commit**

```bash
git add apps/web/app/invoices/ apps/web/components/
git commit -m "Add the booked-invoice list screen"
```

---

## Task 11: Open a booked invoice from the server

Without this, the list is a dead end: clicking an invoice opens the review
screen, which reads `sessionStorage`. In a new browser or a later session that
is empty, so a booked invoice would report "concept niet gevonden" despite being
safely stored.

**Files:**
- Modify: `apps/web/lib/invoices.ts`
- Modify: `apps/web/app/invoices/[invoiceId]/review/ReviewClient.tsx`

- [ ] **Step 1: Add a document fetch to the client**

The document endpoint needs the bearer token, so an `<iframe src>` pointing at
it would be unauthenticated. Fetch it and hand the viewer a blob URL instead.

Append to `apps/web/lib/invoices.ts`:

```typescript
/**
 * Fetches the stored PDF as a blob URL.
 *
 * The endpoint requires the session token, so the URL cannot simply be handed
 * to an iframe: the browser would request it without the header and get a 401.
 * The caller must revoke the returned URL when it is done with it.
 */
export async function documentBlobUrl(invoiceId: string): Promise<string | null> {
  if (!isApiConfigured) return null;

  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/invoices/${invoiceId}/document`, {
      headers: authHeaders(),
    });
    if (!response.ok) return null;
    return URL.createObjectURL(await response.blob());
  } catch {
    return null;
  }
}
```

- [ ] **Step 2: Fall back to the server when the draft is not in this session**

In `ReviewClient.tsx`, add the import:

```tsx
import { documentBlobUrl, getInvoice } from "../../../../lib/invoices";
```

and add this effect after the existing PDF effect:

```tsx
  /* Nothing in this session's storage means the invoice was booked earlier, or
     in another browser. Load it back from the server rather than reporting it
     missing. setState happens in the promise callback, not synchronously in the
     effect body. */
  useEffect(() => {
    if (draft !== null || !invoiceId) return;

    let cancelled = false;

    getInvoice(invoiceId)
      .then((stored) => {
        if (cancelled) return;
        setDraft(stored.canonicalInvoice as unknown as CanonicalInvoiceDraft);
      })
      .catch(() => {
        // Genuinely absent: the existing "not found" card is the right answer.
      });

    return () => {
      cancelled = true;
    };
  }, [draft, invoiceId]);
```

- [ ] **Step 3: Fall back to the stored PDF too**

In the existing PDF effect in `ReviewClient.tsx`, replace the `.then` body so it
tries the server when IndexedDB has nothing:

```tsx
    getPdf(invoiceId)
      .then(async (record) => {
        if (cancelled) return;

        if (record) {
          objectUrl = URL.createObjectURL(record.blob);
          setPdf({ status: "ready", url: objectUrl, name: record.name });
          return;
        }

        // Booked invoices keep their document on the server, so a missing local
        // copy is not the end of the story.
        const fromServer = await documentBlobUrl(invoiceId);
        if (cancelled) return;

        if (fromServer) {
          objectUrl = fromServer;
          setPdf({ status: "ready", url: fromServer, name: "document.pdf" });
        } else {
          setPdf({ status: "missing" });
        }
      })
      .catch(() => {
        if (!cancelled) setPdf({ status: "missing" });
      });
```

- [ ] **Step 4: Verify lint and build**

Run: `cd apps/web && npm run lint && npm run build`
Expected: both exit 0. In particular no `react-hooks/set-state-in-effect`
error — every `setState` above is inside a promise callback, not in the effect
body.

- [ ] **Step 5: Commit**

```bash
git add apps/web/lib/invoices.ts apps/web/app/invoices/
git commit -m "Load a booked invoice and its document back from the server"
```

---

## Task 12: Deploy and verify against the running system

- [ ] **Step 1: Run everything locally**

Run: `dotnet build && dotnet test --nologo`
Expected: 0 warnings, all suites pass.

Run: `cd apps/web && npm run lint && npm run build`
Expected: both exit 0.

- [ ] **Step 2: Push and let CI deploy**

```bash
git push
```

Expected: both workflows run green. The API workflow creates the table and bucket through CloudFormation.

- [ ] **Step 3: Confirm the resources exist**

```bash
aws dynamodb describe-table --table-name xchange-invoices --region eu-central-1 --query "Table.TableStatus" --output text
aws s3 ls | grep xchange-documents
```

Expected: `ACTIVE`, and the bucket is listed.

- [ ] **Step 4: Verify the endpoints are protected**

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://6qh2hyukbn42uq4ffeybjbimku0hhkbo.lambda-url.eu-central-1.on.aws/api/v1/invoices
```

Expected: `401`.

- [ ] **Step 5: Book an invoice through the UI**

Upload a PDF, review it, press **Boeken**, then open **Facturen**.
Expected: the invoice appears in the list, and its number links back to the review screen.

- [ ] **Step 6: Prove it survives the session**

Close the tab, open a new one, sign in, and open the invoice from **Facturen**.
Expected: the fields and the PDF both load - from the server, since
sessionStorage and IndexedDB are empty in a fresh tab. This is the step that
actually tests Task 11; the previous one passes even with everything cached
locally.

- [ ] **Step 7: Confirm the document was stored**

```bash
aws s3 ls s3://xchange-documents-819717422643/ --recursive
```

Expected: one object under the organisation id prefix.

---

## Notes for the implementer

- **Do not weaken the tenant check to make a test pass.** `An_invoice_is_invisible_to_another_organisation` is the most important test here. If it fails, the storage keys are wrong, not the test.
- **The API takes the organisation from the session, never from the request.** If you find yourself adding an `organizationId` parameter to a route, stop and reconsider.
- **Deploy through CI, not by hand.** Manual `update-function-code` was used during earlier debugging and left production ahead of the repository.
- **The 4 MB upload limit is unchanged.** Booking sends the same PDF a second time as base64, which inflates it by a third — still well inside the Lambda request limit at this size, but it is why the limit stays where it is.
