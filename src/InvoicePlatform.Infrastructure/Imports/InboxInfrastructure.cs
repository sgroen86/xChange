using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InvoicePlatform.Infrastructure.Imports;

public sealed class InboxOptions
{
    public const string SectionName = "Inbox";

    public string Domain { get; set; } = "inbox.xchangeinvoice.com";

    /// <summary>The bucket SES writes received mail to, under "inbound/".</summary>
    public string MailBucketName { get; set; } = string.Empty;
}

/// <summary>
/// Inbox addresses in the identity table, both ways:
///
///   pk = INBOX#&lt;localPart&gt;      sk = INBOX   organizationId
///   pk = ORG#&lt;organizationId&gt;  sk = INBOX   localPart
/// </summary>
internal sealed class DynamoDbInboxDirectory(IAmazonDynamoDB dynamo, IOptions<IdentityOptions> options) : IInboxDirectory
{
    private readonly string _table = options.Value.TableName;

    public async Task<string?> FindOrganizationAsync(string localPart, CancellationToken cancellationToken = default)
        => await GetAsync($"INBOX#{localPart}", "organizationId", cancellationToken).ConfigureAwait(false);

    public async Task<string?> GetLocalPartAsync(string organizationId, CancellationToken cancellationToken = default)
        => await GetAsync($"ORG#{organizationId}", "localPart", cancellationToken).ConfigureAwait(false);

    public async Task AssignAsync(string organizationId, string localPart, string? previousLocalPart, CancellationToken cancellationToken = default)
    {
        await dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"INBOX#{localPart}"),
                    ["sk"] = new("INBOX"),
                    ["organizationId"] = new(organizationId),
                },
                // A collision would hand another organisation's mail over; never overwrite.
                ConditionExpression = "attribute_not_exists(pk)",
            },
            cancellationToken).ConfigureAwait(false);

        await dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"ORG#{organizationId}"),
                    ["sk"] = new("INBOX"),
                    ["localPart"] = new(localPart),
                },
            },
            cancellationToken).ConfigureAwait(false);

        if (previousLocalPart is not null)
        {
            await dynamo.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = _table,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new($"INBOX#{previousLocalPart}"),
                        ["sk"] = new("INBOX"),
                    },
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string?> GetAsync(string pk, string attribute, CancellationToken cancellationToken)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = new(pk), ["sk"] = new("INBOX") },
                ConsistentRead = true,
            },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet && response.Item.TryGetValue(attribute, out var value) ? value.S : null;
    }
}

/// <summary>Received mail from the bucket SES writes to, parsed with MimeKit.</summary>
internal sealed class S3ReceivedMailSource(IAmazonS3 s3, IOptions<InboxOptions> options) : IReceivedMailSource
{
    /// <summary>SES accepts mail up to 40 MB; nothing larger is read.</summary>
    private const long MaxMailBytes = 40L * 1024 * 1024;

    public async Task<ReceivedMail?> ReadAsync(string messageId, CancellationToken cancellationToken = default)
    {
        GetObjectResponse response;
        try
        {
            response = await s3.GetObjectAsync(options.Value.MailBucketName, $"inbound/{messageId}", cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        using (response)
        {
            if (response.ContentLength > MaxMailBytes)
            {
                return null;
            }

            await using var stream = response.ResponseStream;
            return await ParseAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Sender, subject and every attachment (including inline PDFs some mail clients produce).</summary>
    internal static async Task<ReceivedMail> ParseAsync(Stream raw, CancellationToken cancellationToken = default)
    {
        var message = await MimeMessage.LoadAsync(raw, cancellationToken).ConfigureAwait(false);
        var attachments = new List<MailAttachment>();

        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            var name = part.FileName;
            var looksLikePdf = part.ContentType.IsMimeType("application", "pdf")
                || (name?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false);
            if (!looksLikePdf || part.Content is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            await part.Content.DecodeToAsync(buffer, cancellationToken).ConfigureAwait(false);
            attachments.Add(new MailAttachment(string.IsNullOrWhiteSpace(name) ? "bijlage.pdf" : Path.GetFileName(name), buffer.ToArray()));
        }

        var sender = message.From.Mailboxes.FirstOrDefault()?.Address;
        return new ReceivedMail(sender, message.Subject, attachments);
    }
}
