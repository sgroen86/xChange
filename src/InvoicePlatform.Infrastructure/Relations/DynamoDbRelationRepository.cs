using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Relations;
using InvoicePlatform.Infrastructure.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Relations;

/// <summary>
/// Relations in the invoice table, in the organisation's own partition:
///
///   pk = ORG#&lt;organizationId&gt;
///   sk = RELATION#&lt;relationId&gt;
///
/// so they are as unreachable from another tenant as its invoices are. Listing
/// is a Query on the partition, never a Scan, paginated because a Limit-free
/// Query still stops at 1 MB.
/// </summary>
internal sealed class DynamoDbRelationRepository(
    IAmazonDynamoDB dynamo,
    IOptions<InvoiceStorageOptions> options)
    : IRelationRepository
{
    private const string SortPrefix = "RELATION#";

    private readonly string _table = options.Value.TableName;

    public async Task<IReadOnlyList<Relation>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var relations = new List<Relation>();
        Dictionary<string, AttributeValue>? startKey = null;

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
                    ExclusiveStartKey = startKey,
                },
                cancellationToken).ConfigureAwait(false);

            relations.AddRange(response.Items.Select(ToRelation));
            startKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (startKey is not null);

        return relations;
    }

    public async Task<Relation?> GetAsync(string organizationId, string relationId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{organizationId}"),
                    ["sk"] = new($"{SortPrefix}{relationId}"),
                },
            },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet ? ToRelation(response.Item) : null;
    }

    public Task SaveAsync(Relation relation, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{relation.OrganizationId}"),
                    ["sk"] = new($"{SortPrefix}{relation.RelationId}"),
                    ["relationId"] = new(relation.RelationId),
                    ["organizationId"] = new(relation.OrganizationId),
                    ["name"] = Nullable(relation.Name),
                    ["vatIdentifier"] = Nullable(relation.VatIdentifier),
                    ["legalRegistrationId"] = Nullable(relation.LegalRegistrationId),
                    ["legalRegistrationScheme"] = Nullable(relation.LegalRegistrationScheme),
                    ["electronicAddress"] = Nullable(relation.ElectronicAddress),
                    ["electronicAddressScheme"] = Nullable(relation.ElectronicAddressScheme),
                    ["countryCode"] = Nullable(relation.CountryCode),
                    ["updatedAt"] = new(relation.UpdatedAt.ToString("O")),
                },
            },
            cancellationToken);

    private static AttributeValue Nullable(string? value)
        => value is null ? new AttributeValue { NULL = true } : new AttributeValue(value);

    private static string? Optional(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) ? value.S : null;

    private static Relation ToRelation(Dictionary<string, AttributeValue> item) => new()
    {
        RelationId = item["relationId"].S,
        OrganizationId = item["organizationId"].S,
        Name = Optional(item, "name"),
        VatIdentifier = Optional(item, "vatIdentifier"),
        LegalRegistrationId = Optional(item, "legalRegistrationId"),
        LegalRegistrationScheme = Optional(item, "legalRegistrationScheme"),
        ElectronicAddress = Optional(item, "electronicAddress"),
        ElectronicAddressScheme = Optional(item, "electronicAddressScheme"),
        CountryCode = Optional(item, "countryCode"),
        UpdatedAt = DateTimeOffset.Parse(item["updatedAt"].S, System.Globalization.CultureInfo.InvariantCulture),
    };
}
