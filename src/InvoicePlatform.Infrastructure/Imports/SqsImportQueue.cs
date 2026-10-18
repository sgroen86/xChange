using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using InvoicePlatform.Application.Imports;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Imports;

public sealed class ImportQueueOptions
{
    public const string SectionName = "Imports";

    /// <summary>The SQS queue the worker consumes; set by the stack.</summary>
    public string QueueUrl { get; set; } = string.Empty;
}

/// <summary>The message the worker receives: which organisation, which import. Nothing else.</summary>
public sealed record ImportMessage(string OrganizationId, string ImportId);

/// <summary>Imports on SQS. The message carries ids only; the PDF and the record stay in S3 and DynamoDB.</summary>
internal sealed class SqsImportQueue(IAmazonSQS sqs, IOptions<ImportQueueOptions> options) : IImportQueue
{
    public Task EnqueueAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Value.QueueUrl))
        {
            throw new InvalidOperationException("Imports:QueueUrl is not configured.");
        }

        return sqs.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = options.Value.QueueUrl,
                MessageBody = JsonSerializer.Serialize(new ImportMessage(organizationId, importId)),
            },
            cancellationToken);
    }
}
