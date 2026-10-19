using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Quality;
using InvoicePlatform.Infrastructure.Learning;
using InvoicePlatform.Infrastructure.Quality;

namespace InvoicePlatform.Integration.Tests;

public class PlatformQualityEndpointTests : IClassFixture<SignedInFactory>
{
    private const string Route = "/api/v1/platform/costs/quality";

    private readonly SignedInFactory _factory;

    public PlatformQualityEndpointTests(SignedInFactory factory) => _factory = factory;

    private static ReadingComparison Reading(params FieldOutcome[] outcomes)
        => new([.. outcomes.Select((o, i) => new FieldComparison($"lines[{i}].unitPrice", "lines[].unitPrice", o))]);

    private static QualityMeasurement Measurement(string organizationId, string model, string at, ReadingComparison read) => new()
    {
        MeasurementId = Guid.NewGuid().ToString("n"),
        OrganizationId = organizationId,
        ExtractionId = "x",
        InvoiceId = "i",
        Model = model,
        Actor = "user-a",
        At = DateTimeOffset.Parse(at),
        Read = read,
        Shown = read,
    };

    [Theory]
    [InlineData("admin-a")]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    [InlineData("admin-b")]
    public async Task Only_the_platform_administrator_sees_reading_quality(string token)
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.As(token).GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task Without_a_login_there_is_nothing()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(Route)).StatusCode);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("march")]
    public async Task A_malformed_month_is_refused(string month)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _factory.As("platform-a").GetAsync($"{Route}?month={month}")).StatusCode);
    }

    [Fact]
    public async Task The_platform_administrator_sees_accuracy_per_model_organisation_and_field()
    {
        await _factory.Platform.SaveOrganizationAsync(new Organization { OrganizationId = SignedInFactory.OrgA, Name = "Org A", CountryCode = "NL", CreatedAt = DateTimeOffset.UnixEpoch });
        await _factory.Quality.RecordAsync(Measurement(SignedInFactory.OrgA, "claude-haiku-5-5", "2024-02-03T10:00:00Z", Reading(FieldOutcome.Correct, FieldOutcome.Correct)));
        await _factory.Quality.RecordAsync(Measurement(SignedInFactory.OrgB, "claude-haiku-5-5", "2024-02-04T10:00:00Z", Reading(FieldOutcome.Correct, FieldOutcome.Missed, FieldOutcome.Correct)));
        await _factory.Ledger.RecordAsync(new UsageEvent
        {
            EventId = Guid.NewGuid().ToString("n"),
            OrganizationId = SignedInFactory.OrgA,
            Actor = "user-a",
            Source = UsageSources.Upload,
            ExtractionId = "x",
            Model = "claude-haiku-5-5",
            Tokens = TokenUsage.None,
            CostUsd = 0.0050m,
            PriceVersion = ModelPrices.Version,
            PriceKnown = true,
            Succeeded = true,
            At = DateTimeOffset.Parse("2024-02-03T09:00:00Z"),
        });

        var month = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>($"{Route}?month=2024-02");

        var read = month.GetProperty("total").GetProperty("read");
        Assert.Equal(2, read.GetProperty("invoices").GetInt64());
        Assert.Equal(1, read.GetProperty("perfectInvoices").GetInt64());
        // Percentages and amounts as strings, never as JSON numbers.
        Assert.Equal("80.0", read.GetProperty("fieldAccuracyPercent").GetString());
        Assert.Equal("50.0", read.GetProperty("perfectPercent").GetString());
        Assert.Equal("0.0050", month.GetProperty("costPerPerfectInvoiceUsd").GetString());
        var model = Assert.Single(month.GetProperty("models").EnumerateArray());
        Assert.Equal("claude-haiku-5-5", model.GetProperty("key").GetString());
        Assert.Equal("0.0050", model.GetProperty("costPerPerfectInvoiceUsd").GetString());
        var organizations = month.GetProperty("organizations").EnumerateArray().ToList();
        Assert.Equal("Org A", organizations.Single(o => o.GetProperty("key").GetString() == SignedInFactory.OrgA).GetProperty("name").GetString());
        var field = Assert.Single(month.GetProperty("fields").EnumerateArray());
        Assert.Equal("lines[].unitPrice", field.GetProperty("field").GetString());
        Assert.Equal(1, field.GetProperty("read").GetProperty("missed").GetInt64());
    }

    [Fact]
    public async Task A_month_without_a_perfect_invoice_has_no_cost_per_perfect_invoice()
    {
        var month = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>($"{Route}?month=2023-01");

        Assert.Equal(JsonValueKind.Null, month.GetProperty("costPerPerfectInvoiceUsd").ValueKind);
        Assert.Equal(JsonValueKind.Null, month.GetProperty("total").GetProperty("read").GetProperty("fieldAccuracyPercent").ValueKind);
    }

    [Fact]
    public async Task Booking_in_the_app_measures_the_reading_of_its_upload()
    {
        var extractionId = Guid.NewGuid().ToString("n");
        var read = new CanonicalInvoiceDraft { InvoiceNumber = "Q-1", CurrencyCode = "EUR" };
        await _factory.Learning.SaveExtractionAsync(SignedInFactory.OrgA, new ExtractionSnapshot(extractionId, read, [], DateTimeOffset.UtcNow) { Model = "claude-haiku-5-5" });

        var response = await _factory.As("user-a").PostAsJsonAsync("/api/v1/invoices", new
        {
            canonicalInvoice = new { invoiceNumber = "Q-2", currencyCode = "EUR" },
            documentBase64 = Convert.ToBase64String([1, 2, 3]),
            sourceFileName = "q.pdf",
            providerModel = "m",
            extractionId,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var measured = Assert.Single(_factory.Quality.Of(SignedInFactory.OrgA), m => m.ExtractionId == extractionId);
        Assert.Equal("claude-haiku-5-5", measured.Model);
        Assert.Equal("user-a", measured.Actor);
        Assert.Contains(measured.Read.Fields, f => f is { Path: "invoiceNumber", Outcome: FieldOutcome.Corrected });
        Assert.Contains(measured.Read.Fields, f => f is { Path: "currencyCode", Outcome: FieldOutcome.Correct });
    }
}

/// <summary>The keys the DynamoDB quality ledger writes: one measurement, five invoice sums, one sum per field.</summary>
public class DynamoDbQualityLedgerKeyTests
{
    private static readonly QualityMeasurement Measured = new()
    {
        MeasurementId = "q1",
        OrganizationId = "org-a",
        ExtractionId = "x1",
        InvoiceId = "i1",
        Model = "claude-haiku-5-5",
        Actor = "user-1",
        At = DateTimeOffset.Parse("2026-10-19T23:30:00+02:00"),
        Read = new([
            new FieldComparison("invoiceNumber", "invoiceNumber", FieldOutcome.Corrected),
            new FieldComparison("lines[0].netAmount", "lines[].netAmount", FieldOutcome.Correct),
            new FieldComparison("lines[1].netAmount", "lines[].netAmount", FieldOutcome.Missed),
        ]),
        Shown = new([
            new FieldComparison("invoiceNumber", "invoiceNumber", FieldOutcome.Correct),
            new FieldComparison("lines[0].netAmount", "lines[].netAmount", FieldOutcome.Correct),
            new FieldComparison("lines[1].netAmount", "lines[].netAmount", FieldOutcome.Correct),
        ]),
    };

    [Fact]
    public void The_measurement_lives_in_the_organisations_partition_and_expires_after_13_months()
    {
        var item = DynamoDbQualityLedger.MeasurementItem(Measured);

        Assert.Equal("ORG#org-a", item["pk"].S);
        Assert.Equal("QUALITY#2026-10-19#q1", item["sk"].S); // UTC day
        Assert.Equal("org-a", item["organizationId"].S);
        Assert.Equal("3", item["readCounted"].N);
        Assert.False(item["readPerfect"].BOOL);
        Assert.True(item["shownPerfect"].BOOL);
        Assert.Contains("\"path\":\"lines[1].netAmount\",\"outcome\":\"missed\"", item["readNotCorrect"].S, StringComparison.Ordinal);
        Assert.Equal("[]", item["shownNotCorrect"].S);
        Assert.Equal(DateTimeOffset.Parse("2027-11-19T21:30:00Z").ToUnixTimeSeconds().ToString(), item["expiresAt"].N);
    }

    [Fact]
    public void Each_measurement_adds_to_its_sums_without_reading_them()
    {
        var updates = DynamoDbQualityLedger.SumUpdates(Measured).ToList();

        Assert.Equal(
            [
                ("ORG#org-a", "QUALITYSUM#2026-10"),
                ("ORG#org-a", "QUALITYSUM#2026-10#MODEL#claude-haiku-5-5"),
                ("PLATFORM", "QUALITYSUM#2026-10"),
                ("PLATFORM", "QUALITYSUM#2026-10#MODEL#claude-haiku-5-5"),
                ("PLATFORM", "QUALITYSUM#2026-10#ORG#org-a"),
                ("PLATFORM", "QUALITYSUM#2026-10#FIELD#invoiceNumber"),
                ("PLATFORM", "QUALITYSUM#2026-10#FIELD#lines[].netAmount"),
            ],
            updates.Select(u => (u.Key["pk"].S, u.Key["sk"].S)));

        Assert.All(updates, u =>
        {
            Assert.Contains("ADD ", u.UpdateExpression, StringComparison.Ordinal);
            Assert.Null(u.ConditionExpression);
        });

        var invoice = updates[2];
        Assert.Equal("1", invoice.ExpressionAttributeValues[":invoices"].N);
        Assert.Equal("0", invoice.ExpressionAttributeValues[":readPerfect"].N);
        Assert.Equal("1", invoice.ExpressionAttributeValues[":shownPerfect"].N);
        Assert.Equal("1", invoice.ExpressionAttributeValues[":readCorrect"].N);

        var lines = updates[6];
        Assert.Equal(("2", "1", "1"), (lines.ExpressionAttributeValues[":readCounted"].N, lines.ExpressionAttributeValues[":readCorrect"].N, lines.ExpressionAttributeValues[":readMissed"].N));
        Assert.False(lines.ExpressionAttributeValues.ContainsKey(":invoices"));

        // Every sum of one organisation says whose it is; the platform-wide ones belong to nobody.
        Assert.All(updates.Where(u => u.Key["pk"].S == "ORG#org-a" || u.Key["sk"].S.Contains("#ORG#", StringComparison.Ordinal)),
            u => Assert.Equal("org-a", u.ExpressionAttributeValues[":org"].S));
        Assert.All(updates.Where(u => u.Key["pk"].S == "PLATFORM" && !u.Key["sk"].S.Contains("#ORG#", StringComparison.Ordinal)),
            u => Assert.False(u.ExpressionAttributeValues.ContainsKey(":org")));
    }

    [Fact]
    public void No_other_prefix_in_the_table_reads_the_quality_keys()
    {
        // The begins_with queries on the invoice table, the cost ledger's, and the quality ledger's own.
        string[] prefixes = ["INVOICE#", "IMPORT#", "RELATION#", "COST#2026-10-", "COSTSUM#2026-10", "QUALITY#2026-10-"];
        var sums = DynamoDbQualityLedger.SumUpdates(Measured).Select(u => u.Key["sk"].S).ToList();

        Assert.All(prefixes, prefix => Assert.DoesNotContain(sums, sk => sk.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.False(DynamoDbQualityLedger.MeasurementItem(Measured)["sk"].S.StartsWith("QUALITYSUM#", StringComparison.Ordinal));
    }
}

/// <summary>The extraction snapshot keeps the model and, where it differs, what the reviewer was shown.</summary>
public class DynamoDbExtractionSnapshotTests
{
    private static readonly CanonicalInvoiceDraft Read = new()
    {
        InvoiceNumber = "F-1",
        Evidence = [new FieldEvidence("invoiceNumber", 0.9m, 1, "F-1")],
    };

    [Fact]
    public void The_model_and_what_the_reviewer_was_shown_survive_the_round_trip()
    {
        var shown = Read with { Seller = new CanonicalParty { ElectronicAddress = "12345678" } };
        var item = DynamoDbLearningStore.ExtractionItem("org-a", new ExtractionSnapshot("x1", Read, [], DateTimeOffset.UtcNow) { Model = "claude-haiku-5-5", Shown = shown });

        var back = DynamoDbLearningStore.ToSnapshot("x1", item);

        Assert.Equal("claude-haiku-5-5", back.Model);
        Assert.Equal("12345678", back.Shown!.Seller!.ElectronicAddress);
        Assert.Empty(back.Shown.Evidence); // the reading keeps it; the copy does not need it
        Assert.Single(back.Extracted.Evidence);
    }

    [Fact]
    public void What_the_reviewer_was_shown_is_not_stored_twice_when_it_is_the_reading()
    {
        var item = DynamoDbLearningStore.ExtractionItem("org-a", new ExtractionSnapshot("x1", Read, [], DateTimeOffset.UtcNow) { Model = "m", Shown = Read with { Evidence = [] } });

        Assert.False(item.ContainsKey("shown"));
        Assert.Null(DynamoDbLearningStore.ToSnapshot("x1", item).Shown);
    }

    [Fact]
    public void A_snapshot_from_before_has_no_model()
    {
        var item = DynamoDbLearningStore.ExtractionItem("org-a", new ExtractionSnapshot("x1", Read, [], DateTimeOffset.UtcNow));

        Assert.Null(DynamoDbLearningStore.ToSnapshot("x1", item).Model);
    }
}
