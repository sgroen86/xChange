using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Quality;
using InvoicePlatform.Infrastructure.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Quality;

/// <summary>
/// Reading quality, in the invoice table, next to the cost ledger:
///
///   pk = ORG#&lt;org&gt;   sk = QUALITY#&lt;yyyy-MM-dd&gt;#&lt;id&gt;               one booking measured (TTL 13 months)
///   pk = ORG#&lt;org&gt;   sk = QUALITYSUM#&lt;yyyy-MM&gt;                     the organisation's month
///   pk = ORG#&lt;org&gt;   sk = QUALITYSUM#&lt;yyyy-MM&gt;#MODEL#&lt;model&gt;       per model
///   pk = PLATFORM    sk = QUALITYSUM#&lt;yyyy-MM&gt;                     the platform's month
///   pk = PLATFORM    sk = QUALITYSUM#&lt;yyyy-MM&gt;#MODEL#&lt;model&gt;       the platform per model
///   pk = PLATFORM    sk = QUALITYSUM#&lt;yyyy-MM&gt;#ORG#&lt;org&gt;           the platform per organisation
///   pk = PLATFORM    sk = QUALITYSUM#&lt;yyyy-MM&gt;#FIELD#&lt;field&gt;       the platform per field (lines[].netAmount)
///
/// Every sum is one UpdateItem ADD, so two bookings at once both count, and a
/// month is one Query on begins_with(QUALITYSUM#&lt;yyyy-MM&gt;). "QUALITY#" and
/// "QUALITYSUM#" differ at their eighth character, so neither prefix matches
/// the other, and no other prefix in this table (INVOICE#, IMPORT#,
/// RELATION#, EXTRACTION#, USAGE#, COST#, COSTSUM#, ALLOWANCE, LEARNING,
/// SETTINGS, INBOX) starts with Q.
///
/// Both measures sit on the same item: "read*" for the model's own reading,
/// "shown*" for what the reviewer was shown.
/// </summary>
internal sealed class DynamoDbQualityLedger(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options) : IQualityLedger
{
    internal const string PlatformKey = "PLATFORM";

    /// <summary>Measurements are kept 13 months, as the cost events are.</summary>
    private const int RetentionMonths = 13;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _table = options.Value.TableName;

    public async Task RecordAsync(QualityMeasurement measurement, CancellationToken cancellationToken = default)
    {
        await dynamo.PutItemAsync(
            new PutItemRequest { TableName = _table, Item = MeasurementItem(measurement) },
            cancellationToken).ConfigureAwait(false);

        await Task.WhenAll(SumUpdates(measurement).Select(update =>
        {
            update.TableName = _table;
            return dynamo.UpdateItemAsync(update, cancellationToken);
        })).ConfigureAwait(false);
    }

    public async Task<PlatformQualityMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        var monthKey = SumPrefix(month);
        var total = QualitySums.Zero;
        var models = new Dictionary<string, QualitySums>(StringComparer.Ordinal);
        var organizations = new Dictionary<string, QualitySums>(StringComparer.Ordinal);
        var fields = new Dictionary<string, FieldSums>(StringComparer.Ordinal);

        foreach (var item in await QueryAsync(PlatformKey, monthKey, cancellationToken).ConfigureAwait(false))
        {
            var rest = item["sk"].S[monthKey.Length..];
            if (rest.Length == 0)
            {
                total = SumsOf(item);
            }
            else if (rest.StartsWith("#MODEL#", StringComparison.Ordinal))
            {
                models[rest["#MODEL#".Length..]] = SumsOf(item);
            }
            else if (rest.StartsWith("#ORG#", StringComparison.Ordinal))
            {
                organizations[rest["#ORG#".Length..]] = SumsOf(item);
            }
            else if (rest.StartsWith("#FIELD#", StringComparison.Ordinal))
            {
                fields[rest["#FIELD#".Length..]] = new FieldSums(OutcomesOf(item, "read"), OutcomesOf(item, "shown"));
            }
        }

        return new PlatformQualityMonth(total, models, organizations, fields);
    }

    internal static string OrgKey(string organizationId) => $"ORG#{organizationId}";

    private static string SumPrefix(UsageMonth month) => $"QUALITYSUM#{month}";

    internal static Dictionary<string, AttributeValue> MeasurementItem(QualityMeasurement m)
    {
        var at = m.At.ToUniversalTime();
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new(OrgKey(m.OrganizationId)),
            ["sk"] = new($"QUALITY#{at:yyyy-MM-dd}#{m.MeasurementId}"),
            ["organizationId"] = new(m.OrganizationId),
            ["measurementId"] = new(m.MeasurementId),
            ["extractionId"] = new(m.ExtractionId),
            ["invoiceId"] = new(m.InvoiceId),
            ["model"] = new(m.Model),
            ["actor"] = new(m.Actor),
            ["at"] = new(at.ToString("O", CultureInfo.InvariantCulture)),
            ["expiresAt"] = Number(at.AddMonths(RetentionMonths).ToUnixTimeSeconds()),
        };

        foreach (var (prefix, reading) in Measures(m))
        {
            var counts = OutcomeCounts.Of(reading.Fields);
            item[$"{prefix}Perfect"] = new AttributeValue { BOOL = reading.Perfect };
            item[$"{prefix}Counted"] = Number(counts.Counted);
            item[$"{prefix}Correct"] = Number(counts.Correct);
            item[$"{prefix}Corrected"] = Number(counts.Corrected);
            item[$"{prefix}Missed"] = Number(counts.Missed);
            item[$"{prefix}Extra"] = Number(counts.Extra);
            // The fields that were not correct, with how: [{"path":"lines[0].unitPrice","outcome":"corrected"}].
            item[$"{prefix}NotCorrect"] = new(JsonSerializer.Serialize(
                reading.NotCorrect.Select(f => new { path = f.Path, outcome = OutcomeName(f.Outcome) }), Json));
        }

        return item;
    }

    /// <summary>The sums one measurement is added to; TableName is set by the caller.</summary>
    internal static IEnumerable<UpdateItemRequest> SumUpdates(QualityMeasurement m)
    {
        var month = SumPrefix(UsageMonth.Of(m.At));
        var org = OrgKey(m.OrganizationId);
        var invoice = new FieldSums(OutcomeCounts.Of(m.Read.Fields), OutcomeCounts.Of(m.Shown.Fields));

        yield return AddInvoice(org, month, m, invoice, m.OrganizationId);
        yield return AddInvoice(org, $"{month}#MODEL#{m.Model}", m, invoice, m.OrganizationId);
        yield return AddInvoice(PlatformKey, month, m, invoice);
        yield return AddInvoice(PlatformKey, $"{month}#MODEL#{m.Model}", m, invoice);
        yield return AddInvoice(PlatformKey, $"{month}#ORG#{m.OrganizationId}", m, invoice, m.OrganizationId);

        foreach (var (field, sums) in FieldSums.Of(m).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            yield return Add(PlatformKey, $"{month}#FIELD#{field}", sums, invoices: null);
        }
    }

    private static UpdateItemRequest AddInvoice(string pk, string sk, QualityMeasurement m, FieldSums fields, string? organizationId = null)
    {
        var update = Add(pk, sk, fields, (m.Read.Perfect, m.Shown.Perfect));
        if (organizationId is not null)
        {
            // On every sum that belongs to one organisation, as on every tenant record.
            update.ExpressionAttributeValues[":org"] = new(organizationId);
            update.UpdateExpression = "SET organizationId = :org " + update.UpdateExpression;
        }

        return update;
    }

    /// <param name="invoices">For an invoice sum: whether each measure was perfect. Null for a field sum.</param>
    private static UpdateItemRequest Add(string pk, string sk, FieldSums sums, (bool Read, bool Shown)? invoices)
    {
        var values = new Dictionary<string, AttributeValue>();
        var names = new Dictionary<string, string>();
        var adds = new List<string>();

        // Names as placeholders: none of them can then collide with a DynamoDB reserved word.
        void Counter(string name, long value)
        {
            values[$":{name}"] = Number(value);
            names[$"#{name}"] = name;
            adds.Add($"#{name} :{name}");
        }

        if (invoices is { } perfect)
        {
            Counter("invoices", 1);
            Counter("readPerfect", perfect.Read ? 1 : 0);
            Counter("shownPerfect", perfect.Shown ? 1 : 0);
        }

        foreach (var (prefix, counts) in new[] { ("read", sums.Read), ("shown", sums.Shown) })
        {
            Counter($"{prefix}Counted", counts.Counted);
            Counter($"{prefix}Correct", counts.Correct);
            Counter($"{prefix}Corrected", counts.Corrected);
            Counter($"{prefix}Missed", counts.Missed);
            Counter($"{prefix}Extra", counts.Extra);
        }

        return new UpdateItemRequest
        {
            Key = new Dictionary<string, AttributeValue> { ["pk"] = new(pk), ["sk"] = new(sk) },
            UpdateExpression = "ADD " + string.Join(", ", adds),
            ExpressionAttributeNames = names,
            ExpressionAttributeValues = values,
        };
    }

    private static (string Prefix, ReadingComparison Reading)[] Measures(QualityMeasurement m) => [("read", m.Read), ("shown", m.Shown)];

    private static string OutcomeName(FieldOutcome outcome) => outcome switch
    {
        FieldOutcome.Correct => "correct",
        FieldOutcome.Corrected => "corrected",
        FieldOutcome.Missed => "missed",
        _ => "extra",
    };

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

    private static QualitySums SumsOf(Dictionary<string, AttributeValue> item)
    {
        var invoices = Long(item, "invoices");
        return new QualitySums(
            new QualityCounts(invoices, Long(item, "readPerfect"), OutcomesOf(item, "read")),
            new QualityCounts(invoices, Long(item, "shownPerfect"), OutcomesOf(item, "shown")));
    }

    private static OutcomeCounts OutcomesOf(Dictionary<string, AttributeValue> item, string prefix) => new(
        Long(item, $"{prefix}Counted"),
        Long(item, $"{prefix}Correct"),
        Long(item, $"{prefix}Corrected"),
        Long(item, $"{prefix}Missed"),
        Long(item, $"{prefix}Extra"));

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static long Long(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) && value.N is { } n ? long.Parse(n, CultureInfo.InvariantCulture) : 0;
}
