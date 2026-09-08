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
/// organisation cannot return another's rows even if the calling code forgot to
/// check. Listing is a Query on the partition, never a Scan.
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
