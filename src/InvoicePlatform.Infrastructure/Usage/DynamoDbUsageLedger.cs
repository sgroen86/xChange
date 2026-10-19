using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Infrastructure.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Usage;

/// <summary>
/// Model calls and their cost, in the invoice table:
///
///   pk = ORG#&lt;org&gt;   sk = COST#&lt;yyyy-MM-dd&gt;#&lt;eventId&gt;            one call (TTL 13 months)
///   pk = ORG#&lt;org&gt;   sk = COSTSUM#&lt;yyyy-MM&gt;                     the organisation's month
///   pk = ORG#&lt;org&gt;   sk = COSTSUM#&lt;yyyy-MM&gt;#USER#&lt;actor&gt;        per user (or "email")
///   pk = ORG#&lt;org&gt;   sk = COSTSUM#&lt;yyyy-MM&gt;#MODEL#&lt;model&gt;       per model
///   pk = PLATFORM    sk = COSTSUM#&lt;yyyy-MM&gt;                     the platform's month
///   pk = PLATFORM    sk = COSTSUM#&lt;yyyy-MM-dd&gt;                  the platform's day
///   pk = PLATFORM    sk = COSTSUM#&lt;yyyy-MM&gt;#MODEL#&lt;model&gt;       the platform per model
///   pk = PLATFORM    sk = COSTSUM#&lt;yyyy-MM&gt;#ORG#&lt;org&gt;           the platform per organisation
///
/// Every sum is one UpdateItem ADD - atomic, no read first - so recording
/// stays cheap and two calls at once both count. A month's sums are one Query
/// on begins_with(COSTSUM#&lt;yyyy-MM&gt;), never a scan. "COST#" and "COSTSUM#"
/// differ at their fifth character, so neither prefix matches the other, and
/// no other prefix in this table (INVOICE#, IMPORT#, RELATION#, EXTRACTION#,
/// USAGE#, ALLOWANCE, LEARNING) starts with COST.
/// </summary>
internal sealed class DynamoDbUsageLedger(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options) : IUsageLedger
{
    internal const string PlatformKey = "PLATFORM";

    /// <summary>Events are kept 13 months: a full year back, plus the month being closed.</summary>
    private const int RetentionMonths = 13;

    private readonly string _table = options.Value.TableName;

    public async Task RecordAsync(UsageEvent usageEvent, CancellationToken cancellationToken = default)
    {
        await dynamo.PutItemAsync(
            new PutItemRequest { TableName = _table, Item = EventItem(usageEvent) },
            cancellationToken).ConfigureAwait(false);

        await Task.WhenAll(SumUpdates(usageEvent).Select(update =>
        {
            update.TableName = _table;
            return dynamo.UpdateItemAsync(update, cancellationToken);
        })).ConfigureAwait(false);
    }

    public async Task<PlatformCostMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        var monthKey = SumPrefix(month);
        var total = CostTotals.Zero;
        var days = new Dictionary<string, CostTotals>(StringComparer.Ordinal);
        var models = new Dictionary<string, CostTotals>(StringComparer.Ordinal);
        var organizations = new Dictionary<string, CostTotals>(StringComparer.Ordinal);

        foreach (var item in await QueryAsync(PlatformKey, monthKey, cancellationToken).ConfigureAwait(false))
        {
            var rest = item["sk"].S[monthKey.Length..];
            var totals = TotalsOf(item);
            if (rest.Length == 0)
            {
                total = totals;
            }
            else if (rest.StartsWith('-'))
            {
                days[$"{month}{rest}"] = totals;
            }
            else if (rest.StartsWith("#MODEL#", StringComparison.Ordinal))
            {
                models[rest["#MODEL#".Length..]] = totals;
            }
            else if (rest.StartsWith("#ORG#", StringComparison.Ordinal))
            {
                organizations[rest["#ORG#".Length..]] = totals;
            }
        }

        return new PlatformCostMonth(total, days, models, organizations);
    }

    public async Task<OrganizationCostMonth> GetOrganizationMonthAsync(string organizationId, UsageMonth month, CancellationToken cancellationToken = default)
    {
        var monthKey = SumPrefix(month);
        var total = CostTotals.Zero;
        var actors = new Dictionary<string, CostTotals>(StringComparer.Ordinal);
        var models = new Dictionary<string, CostTotals>(StringComparer.Ordinal);

        foreach (var item in await QueryAsync(OrgKey(organizationId), monthKey, cancellationToken).ConfigureAwait(false))
        {
            var rest = item["sk"].S[monthKey.Length..];
            var totals = TotalsOf(item);
            if (rest.Length == 0)
            {
                total = totals;
            }
            else if (rest.StartsWith("#USER#", StringComparison.Ordinal))
            {
                actors[rest["#USER#".Length..]] = totals;
            }
            else if (rest.StartsWith("#MODEL#", StringComparison.Ordinal))
            {
                models[rest["#MODEL#".Length..]] = totals;
            }
        }

        return new OrganizationCostMonth(total, actors, models);
    }

    public async Task<IReadOnlyList<UsageEvent>> ListEventsAsync(string organizationId, UsageMonth month, CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(OrgKey(organizationId), $"COST#{month}-", cancellationToken).ConfigureAwait(false);
        return [.. items.Select(ToEvent)];
    }

    internal static string OrgKey(string organizationId) => $"ORG#{organizationId}";

    private static string SumPrefix(UsageMonth month) => $"COSTSUM#{month}";

    internal static Dictionary<string, AttributeValue> EventItem(UsageEvent e)
    {
        var at = e.At.ToUniversalTime();
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new(OrgKey(e.OrganizationId)),
            ["sk"] = new($"COST#{at:yyyy-MM-dd}#{e.EventId}"),
            ["organizationId"] = new(e.OrganizationId),
            ["eventId"] = new(e.EventId),
            ["actor"] = new(e.Actor),
            ["source"] = new(e.Source),
            ["extractionId"] = new(e.ExtractionId),
            ["model"] = new(e.Model),
            ["inputTokens"] = Number(e.Tokens.InputTokens),
            ["outputTokens"] = Number(e.Tokens.OutputTokens),
            ["cacheWriteTokens"] = Number(e.Tokens.CacheWriteTokens),
            ["cacheReadTokens"] = Number(e.Tokens.CacheReadTokens),
            ["costUsd"] = Number(e.CostUsd),
            ["priceVersion"] = new(e.PriceVersion),
            ["priceKnown"] = new AttributeValue { BOOL = e.PriceKnown },
            ["succeeded"] = new AttributeValue { BOOL = e.Succeeded },
            ["at"] = new(at.ToString("O", CultureInfo.InvariantCulture)),
            ["expiresAt"] = Number(at.AddMonths(RetentionMonths).ToUnixTimeSeconds()),
        };
    }

    /// <summary>The seven sums one event is added to; TableName is set by the caller.</summary>
    internal static IEnumerable<UpdateItemRequest> SumUpdates(UsageEvent e)
    {
        var at = e.At.ToUniversalTime();
        var month = SumPrefix(UsageMonth.Of(at));
        var org = OrgKey(e.OrganizationId);

        yield return Add(org, month, e, e.OrganizationId);
        yield return Add(org, $"{month}#USER#{e.Actor}", e, e.OrganizationId);
        yield return Add(org, $"{month}#MODEL#{e.Model}", e, e.OrganizationId);
        yield return Add(PlatformKey, month, e);
        yield return Add(PlatformKey, $"COSTSUM#{at:yyyy-MM-dd}", e);
        yield return Add(PlatformKey, $"{month}#MODEL#{e.Model}", e);
        yield return Add(PlatformKey, $"{month}#ORG#{e.OrganizationId}", e, e.OrganizationId);
    }

    /// <param name="organizationId">On every sum that belongs to one organisation, as on every tenant record.</param>
    private static UpdateItemRequest Add(string pk, string sk, UsageEvent e, string? organizationId = null)
    {
        var values = new Dictionary<string, AttributeValue>
        {
            [":one"] = Number(1),
            [":failed"] = Number(e.Succeeded ? 0 : 1),
            [":in"] = Number(e.Tokens.InputTokens),
            [":out"] = Number(e.Tokens.OutputTokens),
            [":cw"] = Number(e.Tokens.CacheWriteTokens),
            [":cr"] = Number(e.Tokens.CacheReadTokens),
            [":cost"] = Number(e.CostUsd),
        };

        var expression = "ADD calls :one, failedCalls :failed, inputTokens :in, outputTokens :out, "
            + "cacheWriteTokens :cw, cacheReadTokens :cr, costUsd :cost";
        if (organizationId is not null)
        {
            values[":org"] = new(organizationId);
            expression = "SET organizationId = :org " + expression;
        }

        return new UpdateItemRequest
        {
            Key = new Dictionary<string, AttributeValue> { ["pk"] = new(pk), ["sk"] = new(sk) },
            UpdateExpression = expression,
            ExpressionAttributeValues = values,
        };
    }

    private async Task<List<Dictionary<string, AttributeValue>>> QueryAsync(string pk, string sortPrefix, CancellationToken cancellationToken)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
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
                        [":pk"] = new(pk),
                        [":sk"] = new(sortPrefix),
                    },
                    ExclusiveStartKey = start,
                },
                cancellationToken).ConfigureAwait(false);

            items.AddRange(response.Items ?? []);
            start = response.LastEvaluatedKey is { Count: > 0 } last ? last : null;
        }
        while (start is not null);

        return items;
    }

    private static CostTotals TotalsOf(Dictionary<string, AttributeValue> item) => new(
        Long(item, "calls"),
        Long(item, "failedCalls"),
        Long(item, "inputTokens"),
        Long(item, "outputTokens"),
        Long(item, "cacheWriteTokens"),
        Long(item, "cacheReadTokens"),
        Decimal(item, "costUsd"));

    private static UsageEvent ToEvent(Dictionary<string, AttributeValue> item) => new()
    {
        EventId = item["eventId"].S,
        OrganizationId = item["organizationId"].S,
        Actor = item["actor"].S,
        Source = item["source"].S,
        ExtractionId = item["extractionId"].S,
        Model = item["model"].S,
        Tokens = new TokenUsage(Long(item, "inputTokens"), Long(item, "outputTokens"), Long(item, "cacheWriteTokens"), Long(item, "cacheReadTokens")),
        CostUsd = Decimal(item, "costUsd"),
        PriceVersion = item["priceVersion"].S,
        PriceKnown = item["priceKnown"].BOOL ?? false,
        Succeeded = item["succeeded"].BOOL ?? false,
        At = DateTimeOffset.Parse(item["at"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static AttributeValue Number(decimal value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static long Long(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) && value.N is { } n ? long.Parse(n, CultureInfo.InvariantCulture) : 0;

    private static decimal Decimal(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) && value.N is { } n
            ? decimal.Parse(n, NumberStyles.Float, CultureInfo.InvariantCulture)
            : 0m;
}
