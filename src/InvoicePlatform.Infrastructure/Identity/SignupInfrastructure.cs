using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The sender, on an SES-verified domain, e.g. "xChange &lt;noreply@inbox.xchangeinvoice.com&gt;".</summary>
    public string From { get; set; } = "xChange <noreply@inbox.xchangeinvoice.com>";

    /// <summary>Where the app lives, for links in mails.</summary>
    public string AppUrl { get; set; } = "https://app.xchangeinvoice.com";
}

/// <summary>
/// Pending sign-ups in the identity table, expiring by themselves:
///
///   pk = SIGNUP#&lt;sha256 of code&gt;   sk = SIGNUP   (TTL expiresAt)
/// </summary>
internal sealed class DynamoDbSignupStore(IAmazonDynamoDB dynamo, IOptions<IdentityOptions> options) : ISignupStore
{
    private readonly string _table = options.Value.TableName;

    public Task SaveAsync(PendingSignup signup, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"SIGNUP#{signup.CodeHash}"),
                    ["sk"] = new("SIGNUP"),
                    ["email"] = new(signup.Email),
                    ["name"] = new(signup.Name),
                    ["passwordHash"] = new(signup.PasswordHash),
                    ["organizationName"] = new(signup.OrganizationName),
                    ["countryCode"] = new(signup.CountryCode),
                    ["createdAt"] = new(signup.CreatedAt.ToString("O")),
                    // The identity table's TTL attribute: DynamoDB removes it after a day, used or not.
                    ["expiresAt"] = new AttributeValue { N = signup.ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) },
                },
            },
            cancellationToken);

    public async Task<PendingSignup?> TryTakeAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.DeleteItemAsync(
            new DeleteItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"SIGNUP#{codeHash}"),
                    ["sk"] = new("SIGNUP"),
                },
                ReturnValues = ReturnValue.ALL_OLD,
            },
            cancellationToken).ConfigureAwait(false);

        var item = response.Attributes;
        if (item is null || item.Count == 0)
        {
            return null;
        }

        return new PendingSignup(
            codeHash,
            item["email"].S,
            item["name"].S,
            item["passwordHash"].S,
            item["organizationName"].S,
            item["countryCode"].S,
            DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["expiresAt"].N, CultureInfo.InvariantCulture)));
    }
}

/// <summary>Plain-text mail through SES.</summary>
internal sealed class SesEmailSender(IAmazonSimpleEmailServiceV2 ses, IOptions<EmailOptions> options) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        => ses.SendEmailAsync(
            new SendEmailRequest
            {
                FromEmailAddress = options.Value.From,
                Destination = new Destination { ToAddresses = [to] },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = subject, Charset = "UTF-8" },
                        Body = new Body { Text = new Content { Data = body, Charset = "UTF-8" } },
                    },
                },
            },
            cancellationToken);
}
