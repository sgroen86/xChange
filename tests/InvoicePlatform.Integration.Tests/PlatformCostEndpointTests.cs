using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Infrastructure.Usage;

namespace InvoicePlatform.Integration.Tests;

public class PlatformCostEndpointTests : IClassFixture<SignedInFactory>
{
    private static readonly string[] Routes =
    [
        "/api/v1/platform/costs",
        "/api/v1/platform/costs/organizations/org-a",
        "/api/v1/platform/costs/export",
    ];

    private readonly SignedInFactory _factory;

    public PlatformCostEndpointTests(SignedInFactory factory) => _factory = factory;

    private static UsageEvent Event(string organizationId, string actor, string at, decimal cost, string model = "claude-haiku-5-5", bool succeeded = true) => new()
    {
        EventId = Guid.NewGuid().ToString("n"),
        OrganizationId = organizationId,
        Actor = actor,
        Source = UsageSources.Upload,
        ExtractionId = Guid.NewGuid().ToString("n"),
        Model = model,
        Tokens = new TokenUsage(10_000, 2_000, 0, 0),
        CostUsd = cost,
        PriceVersion = ModelPrices.Version,
        PriceKnown = true,
        Succeeded = succeeded,
        At = DateTimeOffset.Parse(at),
    };

    [Theory]
    [InlineData("admin-a")]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    [InlineData("admin-b")]
    public async Task Only_the_platform_administrator_sees_model_cost(string token)
    {
        foreach (var route in Routes)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _factory.As(token).GetAsync(route)).StatusCode);
        }
    }

    [Fact]
    public async Task Without_a_login_there_is_nothing()
    {
        foreach (var route in Routes)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(route)).StatusCode);
        }
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-3")]
    [InlineData("march")]
    [InlineData("")]
    public async Task A_malformed_month_is_refused(string month)
    {
        foreach (var route in Routes)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await _factory.As("platform-a").GetAsync($"{route}?month={month}")).StatusCode);
        }
    }

    [Fact]
    public async Task Without_a_month_it_is_this_month()
    {
        var month = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>("/api/v1/platform/costs");

        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM"), month.GetProperty("month").GetString());
    }

    [Fact]
    public async Task The_platform_administrator_sees_the_month_per_day_model_and_organisation()
    {
        await _factory.Platform.SaveOrganizationAsync(new Organization { OrganizationId = SignedInFactory.OrgA, Name = "Org A", CountryCode = "NL", CreatedAt = DateTimeOffset.UnixEpoch });
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgA, "user-a", "2025-03-02T10:00:00Z", 0.0021m));
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgA, "user-a", "2025-03-03T10:00:00Z", 0.03m, "claude-sonnet-5-5", succeeded: false));
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgB, "admin-b", "2025-03-03T11:00:00Z", 0.0019m));
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgB, "admin-b", "2025-04-01T00:00:00Z", 9m));

        var month = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>("/api/v1/platform/costs?month=2025-03");

        var total = month.GetProperty("total");
        Assert.Equal(3, total.GetProperty("calls").GetInt64());
        Assert.Equal(1, total.GetProperty("failedCalls").GetInt64());
        // Amounts as strings, never as JSON numbers.
        Assert.Equal("0.0340", total.GetProperty("costUsd").GetString());
        Assert.Equal(JsonValueKind.String, total.GetProperty("averageCostUsd").ValueKind);
        Assert.Equal(["2025-03-02", "2025-03-03"], month.GetProperty("days").EnumerateArray().Select(d => d.GetProperty("key").GetString()));
        Assert.Equal(["claude-sonnet-5-5", "claude-haiku-5-5"], month.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("key").GetString()));
        var organizations = month.GetProperty("organizations").EnumerateArray().ToList();
        Assert.Equal([SignedInFactory.OrgA, SignedInFactory.OrgB], organizations.Select(o => o.GetProperty("key").GetString()));
        Assert.Equal("Org A", organizations[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task One_organisations_month_names_its_users_and_lists_its_calls()
    {
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgA, "user-a", "2025-05-02T10:00:00Z", 0.004m));
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgA, UsageActors.Email, "2025-05-02T11:00:00Z", 0.001m));
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgB, "admin-b", "2025-05-02T12:00:00Z", 1m));

        var detail = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>("/api/v1/platform/costs/organizations/org-a?month=2025-05");

        Assert.Equal("0.005", detail.GetProperty("total").GetProperty("costUsd").GetString());
        var users = detail.GetProperty("users").EnumerateArray().ToList();
        Assert.Equal(("user-a", "User A", "user@a"), (users[0].GetProperty("actor").GetString(), users[0].GetProperty("name").GetString(), users[0].GetProperty("email").GetString()));
        Assert.Equal(UsageActors.Email, users[1].GetProperty("actor").GetString());
        var events = detail.GetProperty("topEvents").EnumerateArray().ToList();
        Assert.Equal(2, events.Count); // org-b's call is not org-a's
        Assert.Equal("0.004", events[0].GetProperty("costUsd").GetString());
        Assert.Equal("User A", events[0].GetProperty("actorName").GetString());
    }

    [Fact]
    public async Task A_month_downloads_as_csv()
    {
        await _factory.Ledger.RecordAsync(Event(SignedInFactory.OrgB, "admin-b", "2025-06-10T10:00:00Z", 0.0123m));

        var response = await _factory.As("platform-a").GetAsync("/api/v1/platform/costs/export?month=2025-06");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("xchange-costs-2025-06.csv", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        var lines = (await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("2025-06,org-b,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",0.0123,", lines[1], StringComparison.Ordinal);
    }
}

/// <summary>The keys the DynamoDB ledger writes: one event, seven sums, all added atomically.</summary>
public class DynamoDbUsageLedgerKeyTests
{
    private static readonly UsageEvent Recorded = new()
    {
        EventId = "e1",
        OrganizationId = "org-a",
        Actor = "user-1",
        Source = UsageSources.Upload,
        ExtractionId = "x1",
        Model = "claude-haiku-5-5",
        Tokens = new TokenUsage(100, 20, 3, 4),
        CostUsd = 0.000123m,
        PriceVersion = ModelPrices.Version,
        PriceKnown = true,
        Succeeded = false,
        At = DateTimeOffset.Parse("2026-10-19T23:30:00+02:00"),
    };

    [Fact]
    public void The_event_lives_in_the_organisations_partition_and_expires_after_13_months()
    {
        var item = DynamoDbUsageLedger.EventItem(Recorded);

        Assert.Equal("ORG#org-a", item["pk"].S);
        Assert.Equal("COST#2026-10-19#e1", item["sk"].S); // UTC day
        Assert.Equal("org-a", item["organizationId"].S);
        Assert.Equal("0.000123", item["costUsd"].N);
        Assert.Equal(DateTimeOffset.Parse("2027-11-19T21:30:00Z").ToUnixTimeSeconds().ToString(), item["expiresAt"].N);
    }

    [Fact]
    public void Each_event_adds_to_seven_sums_without_reading_them()
    {
        var updates = DynamoDbUsageLedger.SumUpdates(Recorded).ToList();

        Assert.Equal(
            [
                ("ORG#org-a", "COSTSUM#2026-10"),
                ("ORG#org-a", "COSTSUM#2026-10#USER#user-1"),
                ("ORG#org-a", "COSTSUM#2026-10#MODEL#claude-haiku-5-5"),
                ("PLATFORM", "COSTSUM#2026-10"),
                ("PLATFORM", "COSTSUM#2026-10-19"),
                ("PLATFORM", "COSTSUM#2026-10#MODEL#claude-haiku-5-5"),
                ("PLATFORM", "COSTSUM#2026-10#ORG#org-a"),
            ],
            updates.Select(u => (u.Key["pk"].S, u.Key["sk"].S)));

        Assert.All(updates, u =>
        {
            Assert.Contains("ADD calls :one, failedCalls :failed", u.UpdateExpression, StringComparison.Ordinal);
            Assert.Null(u.ConditionExpression);
            Assert.Equal("1", u.ExpressionAttributeValues[":failed"].N);
            Assert.Equal("0.000123", u.ExpressionAttributeValues[":cost"].N);
        });

        // Every sum of one organisation says whose it is; the platform-wide ones belong to nobody.
        Assert.All(updates.Where(u => u.Key["pk"].S == "ORG#org-a" || u.Key["sk"].S.Contains("#ORG#", StringComparison.Ordinal)),
            u => Assert.Equal("org-a", u.ExpressionAttributeValues[":org"].S));
        Assert.All(updates.Where(u => u.Key["pk"].S == "PLATFORM" && !u.Key["sk"].S.Contains("#ORG#", StringComparison.Ordinal)),
            u => Assert.False(u.ExpressionAttributeValues.ContainsKey(":org")));
    }

    [Fact]
    public void No_other_prefix_in_the_table_reads_the_ledgers_keys()
    {
        // The begins_with queries on the invoice table, and the ledger's own.
        string[] prefixes = ["INVOICE#", "IMPORT#", "RELATION#", "COST#2026-10-"];
        var sums = DynamoDbUsageLedger.SumUpdates(Recorded).Select(u => u.Key["sk"].S).ToList();

        Assert.All(prefixes, prefix => Assert.DoesNotContain(sums, sk => sk.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.False(DynamoDbUsageLedger.EventItem(Recorded)["sk"].S.StartsWith("COSTSUM#", StringComparison.Ordinal));
    }
}
