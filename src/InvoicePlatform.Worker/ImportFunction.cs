using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Infrastructure;
using InvoicePlatform.Infrastructure.Imports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace InvoicePlatform.Worker;

/// <summary>
/// Reads queued imports: one SQS message per PDF, carrying only the
/// organisation and import id. The work itself is ImportService.ProcessAsync,
/// the same extraction the API runs for a single upload.
///
/// A document the model cannot read is recorded on the import, not retried.
/// A message that fails for any other reason (storage, network) is reported
/// back, so SQS retries it and, after three tries, moves it to the
/// dead-letter queue.
/// </summary>
public sealed class ImportFunction
{
    /// <summary>Built once per Lambda instance, like the API's container.</summary>
    private static readonly Lazy<IServiceProvider> Services = new(Build);

    private static IServiceProvider Build()
    {
        // Configuration as the API reads it in Lambda: from environment
        // variables, with "__" for nesting (Anthropic__Model and so on).
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging => logging.AddConsole());
        services.AddInvoiceExtraction(configuration);
        services.AddIdentity(configuration);
        services.AddInvoiceStorage(configuration);
        return services.BuildServiceProvider();
    }

    public async Task<SQSBatchResponse> HandleAsync(SQSEvent sqsEvent, ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(sqsEvent);
        ArgumentNullException.ThrowIfNull(context);

        var logger = Services.Value.GetRequiredService<ILogger<ImportFunction>>();
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        // Stop a little before Lambda would, so the import is left in a state that says so.
        using var deadline = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(10));

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                var message = JsonSerializer.Deserialize<ImportMessage>(record.Body)
                    ?? throw new InvalidOperationException("Empty import message.");

                await using var scope = Services.Value.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ImportService>()
                    .ProcessAsync(message.OrganizationId, message.ImportId, deadline.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Import message {MessageId} failed; SQS will retry it.", record.MessageId);
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse(failures);
    }
}
