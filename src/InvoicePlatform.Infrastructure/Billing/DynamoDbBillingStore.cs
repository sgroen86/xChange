using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Infrastructure.Invoices;
using InvoicePlatform.Infrastructure.Usage;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Billing;

/// <summary>
/// Billing state in the invoice table. The organisation's own state is on its
/// allowance item (<see cref="AllowanceItem"/>), in its own partition; two
/// kinds of pointer live outside any tenant:
///
///   pk = STRIPE#CUSTOMER#&lt;customerId&gt;   sk = CUSTOMER   organizationId   (a webhook's customer -> organisation)
///   pk = STRIPE#EVENT#&lt;key&gt;             sk = EVENT      applied once     (expires after 30 days)
///
/// Each webhook write puts its marker and its change in one transaction, so a
/// retried event is either fully applied once or not at all.
/// </summary>
internal sealed class DynamoDbBillingStore(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options, IClock clock) : IBillingStore
{
    /// <summary>Stripe retries for three days; the marker outlives that comfortably.</summary>
    private static readonly TimeSpan MarkerLifetime = TimeSpan.FromDays(30);

    private readonly string _table = options.Value.TableName;

    public async Task<BillingAccount> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = AllowanceItem.Key(organizationId), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsItemSet)
        {
            return BillingAccount.None;
        }

        var item = response.Item;
        return new BillingAccount(
            CustomerId: AllowanceItem.String(item, "stripeCustomerId"),
            SubscriptionId: AllowanceItem.String(item, "stripeSubscriptionId"),
            PlanId: AllowanceItem.String(item, "subscriptionPlan"),
            Status: AllowanceItem.String(item, "subscriptionStatus"),
            PeriodStart: Time(AllowanceItem.Long(item, "periodStart")),
            PeriodEnd: Time(AllowanceItem.Long(item, "periodEnd")),
            CancelAtPeriodEnd: AllowanceItem.Bool(item, "cancelAtPeriodEnd"),
            PeriodAllowance: AllowanceItem.Int(item, "periodAllowance"),
            PeriodUsed: AllowanceItem.Int(item, "periodUsed") ?? 0,
            Credits: AllowanceItem.Int(item, "credits") ?? 0);
    }

    public async Task<string?> FindOrganizationByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = CustomerKey(customerId), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet ? AllowanceItem.String(response.Item, "organizationId") : null;
    }

    public async Task<string> LinkCustomerAsync(string organizationId, string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.TransactWriteItemsAsync(
                new TransactWriteItemsRequest
                {
                    TransactItems =
                    [
                        new TransactWriteItem
                        {
                            Update = new Update
                            {
                                TableName = _table,
                                Key = AllowanceItem.Key(organizationId),
                                UpdateExpression = "SET stripeCustomerId = :customer",
                                ConditionExpression = "attribute_not_exists(stripeCustomerId) OR stripeCustomerId = :customer",
                                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":customer"] = new(customerId) },
                            },
                        },
                        new TransactWriteItem
                        {
                            Put = new Put
                            {
                                TableName = _table,
                                Item = new Dictionary<string, AttributeValue>(CustomerKey(customerId))
                                {
                                    ["organizationId"] = new(organizationId),
                                    ["customerId"] = new(customerId),
                                },
                                ConditionExpression = "attribute_not_exists(pk) OR organizationId = :org",
                                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":org"] = new(organizationId) },
                            },
                        },
                    ],
                },
                cancellationToken).ConfigureAwait(false);
            return customerId;
        }
        catch (TransactionCanceledException canceled)
        {
            if (Failed(canceled, 1))
            {
                throw new InvalidOperationException($"Customer {customerId} already belongs to another organisation.", canceled);
            }

            if (Failed(canceled, 0))
            {
                // The organisation linked another customer first; it keeps that one.
                var account = await GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
                return account.CustomerId ?? throw new InvalidOperationException("Linking the customer failed.", canceled);
            }

            throw;
        }
    }

    public Task<BillingWrite> AddCreditsAsync(string organizationId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
        => OnceAsync(
            idempotencyKey,
            new Update
            {
                TableName = _table,
                Key = AllowanceItem.Key(organizationId),
                UpdateExpression = "SET paidOnce = :true ADD credits :credits",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":true"] = new AttributeValue { BOOL = true },
                    [":credits"] = Number(credits),
                },
            },
            cancellationToken);

    public Task<BillingWrite> ApplySubscriptionAsync(
        string organizationId,
        SubscriptionState state,
        DateTimeOffset eventCreated,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, AttributeValue>
        {
            [":sub"] = new(state.SubscriptionId),
            [":status"] = new(state.Status),
            [":cancel"] = new AttributeValue { BOOL = state.CancelAtPeriodEnd },
            [":at"] = Number(eventCreated.ToUnixTimeSeconds()),
        };

        const string NotOlder = "(attribute_not_exists(subscriptionEventAt) OR subscriptionEventAt <= :at)";

        Update update;
        if (state.MonthlyConversions is { } conversions && state.PlanId is not null)
        {
            values[":plan"] = new(state.PlanId);
            values[":allowance"] = Number(conversions);
            values[":zero"] = Number(0);
            values[":true"] = new AttributeValue { BOOL = true };

            var set = "SET stripeSubscriptionId = :sub, subscriptionStatus = :status, cancelAtPeriodEnd = :cancel, subscriptionEventAt = :at, "
                + "subscriptionPlan = :plan, periodAllowance = :allowance, periodUsed = if_not_exists(periodUsed, :zero), "
                + "subscribedOnce = :true, paidOnce = :true";
            if (state.PeriodStart is { } start)
            {
                values[":start"] = Number(start.ToUnixTimeSeconds());
                set += ", periodStart = :start";
            }

            if (state.PeriodEnd is { } end)
            {
                values[":end"] = Number(end.ToUnixTimeSeconds());
                set += ", periodEnd = :end";
            }

            update = new Update
            {
                TableName = _table,
                Key = AllowanceItem.Key(organizationId),
                UpdateExpression = set,
                ConditionExpression = NotOlder,
                ExpressionAttributeValues = values,
            };
        }
        else
        {
            // Ended, unpaid or not started: no plan, no period conversions.
            // Purchased credits stay. Only for the subscription the
            // organisation has: an old one ending never takes a new one's plan.
            update = new Update
            {
                TableName = _table,
                Key = AllowanceItem.Key(organizationId),
                UpdateExpression = "SET stripeSubscriptionId = :sub, subscriptionStatus = :status, cancelAtPeriodEnd = :cancel, subscriptionEventAt = :at "
                    + "REMOVE subscriptionPlan, periodAllowance, periodStart, periodEnd",
                ConditionExpression = NotOlder + " AND (attribute_not_exists(stripeSubscriptionId) OR stripeSubscriptionId = :sub)",
                ExpressionAttributeValues = values,
            };
        }

        return OnceAsync(idempotencyKey, update, cancellationToken);
    }

    public Task<BillingWrite> StartPeriodAsync(
        string organizationId,
        DateTimeOffset periodStart,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => OnceAsync(
            idempotencyKey,
            new Update
            {
                TableName = _table,
                Key = AllowanceItem.Key(organizationId),
                UpdateExpression = "SET periodUsed = :zero, periodResetFrom = :start",
                ConditionExpression = "attribute_not_exists(periodResetFrom) OR periodResetFrom < :start",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":zero"] = Number(0),
                    [":start"] = Number(periodStart.ToUnixTimeSeconds()),
                },
            },
            cancellationToken);

    public Task<BillingWrite> MarkPastDueAsync(
        string organizationId,
        string subscriptionId,
        DateTimeOffset eventCreated,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => OnceAsync(
            idempotencyKey,
            new Update
            {
                TableName = _table,
                Key = AllowanceItem.Key(organizationId),
                UpdateExpression = "SET subscriptionStatus = :status, subscriptionEventAt = :at",
                ConditionExpression = "stripeSubscriptionId = :sub AND (attribute_not_exists(subscriptionEventAt) OR subscriptionEventAt <= :at)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new(SubscriptionStatuses.PastDue),
                    [":sub"] = new(subscriptionId),
                    [":at"] = Number(eventCreated.ToUnixTimeSeconds()),
                },
            },
            cancellationToken);

    /// <summary>The marker and the change, in one transaction: both happen, or neither.</summary>
    private async Task<BillingWrite> OnceAsync(string idempotencyKey, Update update, CancellationToken cancellationToken)
    {
        try
        {
            await dynamo.TransactWriteItemsAsync(
                new TransactWriteItemsRequest
                {
                    TransactItems =
                    [
                        new TransactWriteItem
                        {
                            Put = new Put
                            {
                                TableName = _table,
                                Item = new Dictionary<string, AttributeValue>
                                {
                                    ["pk"] = new($"STRIPE#EVENT#{idempotencyKey}"),
                                    ["sk"] = new("EVENT"),
                                    ["appliedAt"] = new(clock.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                                    ["expiresAt"] = Number((clock.UtcNow + MarkerLifetime).ToUnixTimeSeconds()),
                                },
                                ConditionExpression = "attribute_not_exists(pk)",
                            },
                        },
                        new TransactWriteItem { Update = update },
                    ],
                },
                cancellationToken).ConfigureAwait(false);
            return BillingWrite.Applied;
        }
        catch (TransactionCanceledException canceled) when (Failed(canceled, 0))
        {
            return BillingWrite.Duplicate;
        }
        catch (TransactionCanceledException canceled) when (Failed(canceled, 1))
        {
            return BillingWrite.Stale;
        }
    }

    private static bool Failed(TransactionCanceledException canceled, int index)
        => canceled.CancellationReasons is { } reasons
            && reasons.Count > index
            && reasons[index].Code == "ConditionalCheckFailed";

    private static Dictionary<string, AttributeValue> CustomerKey(string customerId) => new()
    {
        ["pk"] = new($"STRIPE#CUSTOMER#{customerId}"),
        ["sk"] = new("CUSTOMER"),
    };

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static DateTimeOffset? Time(long? seconds) => seconds is { } s ? DateTimeOffset.FromUnixTimeSeconds(s) : null;
}
