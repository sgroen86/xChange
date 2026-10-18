using Amazon.Lambda.Core;
using Amazon.Lambda.SimpleEmailEvents;
using Amazon.Lambda.SimpleEmailEvents.Actions;
using InvoicePlatform.Application.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoicePlatform.Worker;

/// <summary>
/// Called by SES for every mail to the inbox domain, after SES has stored the
/// raw message in the mail bucket. Mail SES marks as a virus or spam is
/// dropped; otherwise every PDF attachment becomes an import for the
/// organisation whose address it was sent to, and is read by ImportFunction.
/// </summary>
public sealed class MailFunction
{
    public async Task HandleAsync(SimpleEmailEvent<LambdaReceiptAction> sesEvent, ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(sesEvent);
        ArgumentNullException.ThrowIfNull(context);

        var services = ImportFunction.Services.Value;
        var logger = services.GetRequiredService<ILogger<MailFunction>>();
        using var deadline = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(5));

        foreach (var record in sesEvent.Records)
        {
            var mail = record.Ses.Mail;
            var receipt = record.Ses.Receipt;

            if (Failed(receipt.VirusVerdict?.Status) || Failed(receipt.SpamVerdict?.Status))
            {
                logger.LogWarning("Mail {MessageId} dropped: virus {Virus}, spam {Spam}.",
                    mail.MessageId, receipt.VirusVerdict?.Status, receipt.SpamVerdict?.Status);
                continue;
            }

            await using var scope = services.CreateAsyncScope();
            var created = await scope.ServiceProvider.GetRequiredService<InboxService>()
                .ReceiveAsync(mail.MessageId, receipt.Recipients, deadline.Token)
                .ConfigureAwait(false);

            logger.LogInformation("Mail {MessageId}: {Count} PDF(s) queued.", mail.MessageId, created);
        }
    }

    private static bool Failed(string? status) => string.Equals(status, "FAIL", StringComparison.OrdinalIgnoreCase);
}
