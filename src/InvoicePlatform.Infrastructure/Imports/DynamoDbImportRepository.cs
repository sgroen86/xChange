using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Imports;
using ImportStatus = InvoicePlatform.Domain.Imports.ImportStatus;
using InvoicePlatform.Domain.Validation;
using InvoicePlatform.Infrastructure.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Imports;

/// <summary>
/// Imports in the invoice table, in the organisation's partition:
///
///   pk = ORG#&lt;organizationId&gt;   sk = IMPORT#&lt;importId&gt;
///
/// Listing is a Query on the partition, never a Scan.
/// </summary>
internal sealed class DynamoDbImportRepository(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options) : IImportRepository
{
    private const string SortPrefix = "IMPORT#";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string _table = options.Value.TableName;

    public Task SaveAsync(ImportRecord record, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{record.OrganizationId}"),
                    ["sk"] = new($"{SortPrefix}{record.ImportId}"),
                    ["importId"] = new(record.ImportId),
                    ["organizationId"] = new(record.OrganizationId),
                    ["source"] = new(record.Source.ToString()),
                    ["status"] = new(record.Status.ToString()),
                    ["fileName"] = new(record.FileName),
                    ["byteSize"] = new AttributeValue { N = record.ByteSize.ToString(CultureInfo.InvariantCulture) },
                    ["documentKey"] = new(record.DocumentKey),
                    ["sender"] = Nullable(record.Sender),
                    ["subject"] = Nullable(record.Subject),
                    ["extractionId"] = Nullable(record.ExtractionId),
                    ["draft"] = Nullable(record.Draft is null ? null : JsonSerializer.Serialize(record.Draft, Json)),
                    ["warnings"] = new(JsonSerializer.Serialize(record.Warnings, Json)),
                    ["providerModel"] = Nullable(record.ProviderModel),
                    ["error"] = Nullable(record.Error),
                    ["rulesetId"] = Nullable(record.RulesetId),
                    ["violations"] = new(JsonSerializer.Serialize(record.Violations, Json)),
                    ["createdAt"] = new(record.CreatedAt.ToString("O")),
                    ["createdByUserId"] = Nullable(record.CreatedByUserId),
                    ["updatedAt"] = new(record.UpdatedAt.ToString("O")),
                },
            },
            cancellationToken);

    public async Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest
            {
                TableName = _table,
                Key = Key(organizationId, importId),
                ConsistentRead = true,
            },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet ? ToRecord(response.Item) : null;
    }

    public async Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var records = new List<ImportRecord>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(
                new QueryRequest
                {
                    TableName = _table,
                    KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":pk"] = new($"ORG#{organizationId}"),
                        [":sk"] = new(SortPrefix),
                    },
                    ExclusiveStartKey = start,
                },
                cancellationToken).ConfigureAwait(false);

            records.AddRange(response.Items.Select(ToRecord));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return records;
    }

    public Task DeleteAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        => dynamo.DeleteItemAsync(new DeleteItemRequest { TableName = _table, Key = Key(organizationId, importId) }, cancellationToken);

    private static Dictionary<string, AttributeValue> Key(string organizationId, string importId) => new()
    {
        ["pk"] = new($"ORG#{organizationId}"),
        ["sk"] = new($"{SortPrefix}{importId}"),
    };

    private static AttributeValue Nullable(string? value)
        => value is null ? new AttributeValue { NULL = true } : new AttributeValue(value);

    private static string? Optional(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) ? value.S : null;

    private static ImportRecord ToRecord(Dictionary<string, AttributeValue> item) => new()
    {
        ImportId = item["importId"].S,
        OrganizationId = item["organizationId"].S,
        Source = Enum.TryParse<ImportSource>(Optional(item, "source"), out var source) ? source : ImportSource.Upload,
        Status = Enum.TryParse<ImportStatus>(Optional(item, "status"), out var status) ? status : ImportStatus.Failed,
        FileName = Optional(item, "fileName") ?? "document.pdf",
        ByteSize = long.TryParse(item.GetValueOrDefault("byteSize")?.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) ? size : 0,
        DocumentKey = item["documentKey"].S,
        Sender = Optional(item, "sender"),
        Subject = Optional(item, "subject"),
        ExtractionId = Optional(item, "extractionId"),
        Draft = Optional(item, "draft") is { } draft ? JsonSerializer.Deserialize<CanonicalInvoiceDraft>(draft, Json) : null,
        Warnings = Optional(item, "warnings") is { } warnings
            ? JsonSerializer.Deserialize<List<ValidationWarning>>(warnings, Json) ?? []
            : [],
        ProviderModel = Optional(item, "providerModel"),
        Error = Optional(item, "error"),
        RulesetId = Optional(item, "rulesetId"),
        Violations = Optional(item, "violations") is { } violations
            ? JsonSerializer.Deserialize<List<RuleViolation>>(violations, Json) ?? []
            : [],
        CreatedAt = DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
        CreatedByUserId = Optional(item, "createdByUserId"),
        UpdatedAt = Optional(item, "updatedAt") is { } updated ? DateTimeOffset.Parse(updated, CultureInfo.InvariantCulture) : default,
    };
}
