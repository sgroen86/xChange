using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Infrastructure.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Learning;

/// <summary>
/// Learning data in the invoice table, in the organisation's partition:
///
///   pk = ORG#&lt;organizationId&gt;   sk = EXTRACTION#&lt;extractionId&gt;   what the model read (TTL 30 days)
///   pk = ORG#&lt;organizationId&gt;   sk = LEARNING                      the remembered corrections
///
/// Both are read only with the organisation from the session, so another
/// tenant's corrections are unreachable.
/// </summary>
internal sealed class DynamoDbLearningStore(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options) : ILearningStore
{
    private static readonly TimeSpan ExtractionLifetime = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string _table = options.Value.TableName;

    public Task SaveExtractionAsync(string organizationId, ExtractionSnapshot snapshot, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{organizationId}"),
                    ["sk"] = new($"EXTRACTION#{snapshot.ExtractionId}"),
                    ["extracted"] = new(JsonSerializer.Serialize(snapshot.Extracted, Json)),
                    ["applied"] = new(JsonSerializer.Serialize(snapshot.AppliedCorrectionIds, Json)),
                    ["createdAt"] = new(snapshot.CreatedAt.ToString("O")),
                    // DynamoDB deletes it after this moment (epoch seconds), read or not.
                    ["expiresAt"] = new AttributeValue
                    {
                        N = snapshot.CreatedAt.Add(ExtractionLifetime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    },
                },
            },
            cancellationToken);

    public async Task<ExtractionSnapshot?> TakeExtractionAsync(string organizationId, string extractionId, CancellationToken cancellationToken = default)
    {
        // Read and delete in one call: two bookings of the same upload cannot both learn from it.
        var response = await dynamo.DeleteItemAsync(
            new DeleteItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{organizationId}"),
                    ["sk"] = new($"EXTRACTION#{extractionId}"),
                },
                ReturnValues = ReturnValue.ALL_OLD,
            },
            cancellationToken).ConfigureAwait(false);

        var item = response.Attributes;
        if (item is null || item.Count == 0)
        {
            return null;
        }

        // TTL deletion is lazy: an expired snapshot can still be there for a while.
        if (item.TryGetValue("expiresAt", out var expires)
            && long.TryParse(expires.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            && DateTimeOffset.FromUnixTimeSeconds(seconds) < DateTimeOffset.UtcNow)
        {
            return null;
        }

        return new ExtractionSnapshot(
            extractionId,
            JsonSerializer.Deserialize<CanonicalInvoiceDraft>(item["extracted"].S, Json)!,
            JsonSerializer.Deserialize<List<string>>(item["applied"].S, Json) ?? [],
            DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<LearnedCorrection>> GetCorrectionsAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest
            {
                TableName = _table,
                Key = LearningKey(organizationId),
                ConsistentRead = true,
            },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet && response.Item.TryGetValue("corrections", out var value)
            ? JsonSerializer.Deserialize<List<LearnedCorrection>>(value.S, Json) ?? []
            : [];
    }

    public Task SaveCorrectionsAsync(string organizationId, IReadOnlyList<LearnedCorrection> corrections, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>(LearningKey(organizationId))
                {
                    ["corrections"] = new(JsonSerializer.Serialize(corrections, Json)),
                    ["updatedAt"] = new(DateTimeOffset.UtcNow.ToString("O")),
                },
            },
            cancellationToken);

    private static Dictionary<string, AttributeValue> LearningKey(string organizationId) => new()
    {
        ["pk"] = new($"ORG#{organizationId}"),
        ["sk"] = new("LEARNING"),
    };
}
