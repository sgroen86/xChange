using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Infrastructure.Quality;
using InvoicePlatform.Infrastructure.Usage;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Invoices;

/// <summary>
/// An organisation's items in the invoice table, deleted when the
/// organisation is:
///
///   pk = ORG#&lt;org&gt;                 everything: INVOICE#, IMPORT#, RELATION#, EXTRACTION#, LEARNING,
///                                ALLOWANCE (billing), USAGE#, COST#, COSTSUM#, QUALITY#, QUALITYSUM#
///   pk = PLATFORM  sk = COSTSUM#&lt;yyyy-MM&gt;#ORG#&lt;org&gt;      the platform's cost per organisation
///   pk = PLATFORM  sk = QUALITYSUM#&lt;yyyy-MM&gt;#ORG#&lt;org&gt;   the platform's quality per organisation
///   pk = STRIPE#CUSTOMER#&lt;id&gt;   sk = CUSTOMER          the customer pointer
///
/// The PLATFORM totals per month, day, model and field stay: they are
/// aggregates. Processed-event markers (STRIPE#EVENT#) name no organisation
/// and expire by themselves.
/// </summary>
internal sealed class DynamoDbOrganizationRecordDeletion(
    IAmazonDynamoDB dynamo,
    IOptions<InvoiceStorageOptions> options)
    : IOrganizationRecordDeletion
{
    private readonly string _table = options.Value.TableName;

    public async Task<int> DeletePlatformSumsAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        // A filter on the organisation (set on exactly these sums, see the
        // ledgers) within the PLATFORM partition: a Query, never a Scan. The
        // sort key is checked again before anything is deleted.
        var suffix = $"#ORG#{organizationId}";
        bool NotThisOrganization(Dictionary<string, AttributeValue> item) => !item["sk"].S.EndsWith(suffix, StringComparison.Ordinal);

        var deleted = 0;
        foreach (var (pk, prefix) in new[] { (DynamoDbUsageLedger.PlatformKey, "COSTSUM#"), (DynamoDbQualityLedger.PlatformKey, "QUALITYSUM#") })
        {
            deleted += await DynamoDbBatchDelete.DeleteQueriedAsync(
                dynamo,
                new QueryRequest
                {
                    TableName = _table,
                    KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
                    FilterExpression = "organizationId = :org",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":pk"] = new(pk),
                        [":sk"] = new(prefix),
                        [":org"] = new(organizationId),
                    },
                },
                cancellationToken,
                keep: NotThisOrganization).ConfigureAwait(false);
        }

        return deleted;
    }

    public async Task<int> DeleteCustomerLinkAsync(string organizationId, string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = _table,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new($"STRIPE#CUSTOMER#{customerId}"),
                        ["sk"] = new("CUSTOMER"),
                    },
                    // Only a pointer at this organisation.
                    ConditionExpression = "organizationId = :org",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":org"] = new(organizationId) },
                },
                cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (ConditionalCheckFailedException)
        {
            return 0;
        }
    }

    public Task<int> DeleteAllForOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        => DynamoDbBatchDelete.DeleteQueriedAsync(
            dynamo,
            new QueryRequest
            {
                TableName = _table,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new($"ORG#{organizationId}") },
            },
            cancellationToken);
}
