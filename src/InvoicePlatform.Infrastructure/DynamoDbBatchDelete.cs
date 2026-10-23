using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace InvoicePlatform.Infrastructure;

/// <summary>
/// Deleting many items from one of the tables (both use "pk"/"sk"): a Query,
/// page by page, and BatchWriteItem in groups of 25 - DynamoDB's maximum -
/// retrying what it leaves unprocessed, with a growing pause, until none is
/// left. Used when an organisation is deleted.
/// </summary>
internal static class DynamoDbBatchDelete
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 8;

    /// <summary>
    /// Deletes every item the query finds. The query's projection is set here
    /// (keys only); its pagination is followed to the end.
    /// </summary>
    /// <param name="keep">Items for which this returns true are left alone: a second check on what a filter found.</param>
    /// <returns>How many items were deleted.</returns>
    public static async Task<int> DeleteQueriedAsync(
        IAmazonDynamoDB dynamo,
        QueryRequest query,
        CancellationToken cancellationToken,
        Func<Dictionary<string, AttributeValue>, bool>? keep = null)
    {
        query.ProjectionExpression = "#delPk, #delSk";
        query.ExpressionAttributeNames ??= [];
        query.ExpressionAttributeNames["#delPk"] = "pk";
        query.ExpressionAttributeNames["#delSk"] = "sk";

        var deleted = 0;
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            query.ExclusiveStartKey = start;
            var response = await dynamo.QueryAsync(query, cancellationToken).ConfigureAwait(false);

            var keys = (response.Items ?? [])
                .Where(item => keep is null || !keep(item))
                .Select(item => new Dictionary<string, AttributeValue> { ["pk"] = item["pk"], ["sk"] = item["sk"] })
                .ToList();
            deleted += await DeleteKeysAsync(dynamo, query.TableName, keys, cancellationToken).ConfigureAwait(false);

            // Continuing after deleting what the page found is safe: the start
            // key need not exist any more.
            start = response.LastEvaluatedKey is { Count: > 0 } last ? last : null;
        }
        while (start is not null);

        return deleted;
    }

    /// <summary>Deletes the items with these keys (each key once).</summary>
    /// <returns>How many keys were deleted.</returns>
    public static async Task<int> DeleteKeysAsync(
        IAmazonDynamoDB dynamo,
        string table,
        IReadOnlyList<Dictionary<string, AttributeValue>> keys,
        CancellationToken cancellationToken)
    {
        foreach (var batch in keys.Chunk(BatchSize))
        {
            var pending = new Dictionary<string, List<WriteRequest>>
            {
                [table] = [.. batch.Select(key => new WriteRequest { DeleteRequest = new DeleteRequest { Key = key } })],
            };

            for (var attempt = 1; ; attempt++)
            {
                var response = await dynamo.BatchWriteItemAsync(
                    new BatchWriteItemRequest { RequestItems = pending },
                    cancellationToken).ConfigureAwait(false);

                var left = response.UnprocessedItems?.Values.Sum(requests => requests.Count) ?? 0;
                if (left == 0)
                {
                    break;
                }

                if (attempt >= MaxAttempts)
                {
                    throw new InvalidOperationException(
                        $"DynamoDB left {left} deletes in {table} unprocessed after {MaxAttempts} attempts; run the deletion again.");
                }

                pending = response.UnprocessedItems!;
                // Unprocessed items mean throttling: back off before retrying (100 ms, 200 ms, ... up to 3.2 s).
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(3200, 50 << attempt)), cancellationToken).ConfigureAwait(false);
            }
        }

        return keys.Count;
    }
}
