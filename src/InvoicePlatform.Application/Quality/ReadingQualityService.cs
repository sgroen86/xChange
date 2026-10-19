using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Quality;
using Microsoft.Extensions.Logging;

namespace InvoicePlatform.Application.Quality;

/// <summary>
/// Measures how well an invoice was read, when a reviewer books it: the
/// booking is compared with the extraction snapshot that learning takes at the
/// same moment. Only a first booking has a snapshot, so each upload is
/// measured once.
/// </summary>
public sealed class ReadingQualityService(IQualityLedger ledger, IClock clock, ILogger<ReadingQualityService> logger)
{
    /// <summary>
    /// Compares and records. Never fails the booking: the customer's invoice
    /// matters more than our measurement, so a failure is logged and booking
    /// goes on.
    /// </summary>
    /// <param name="actor">The user who reviewed and booked the invoice.</param>
    /// <param name="booked">The draft as the reviewer left it (before the calculator).</param>
    public async Task MeasureAsync(
        string organizationId,
        string invoiceId,
        string actor,
        ExtractionSnapshot snapshot,
        CanonicalInvoiceDraft booked)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            // Kept before the model was recorded with it: unknown which model to credit.
            if (snapshot.Model is not { Length: > 0 } model)
            {
                return;
            }

            var shown = snapshot.Shown ?? snapshot.Extracted;

            // Not cancelled with the request: the booking has happened.
            await ledger.RecordAsync(
                new QualityMeasurement
                {
                    MeasurementId = Guid.NewGuid().ToString("n"),
                    OrganizationId = organizationId,
                    ExtractionId = snapshot.ExtractionId,
                    InvoiceId = invoiceId,
                    Model = model,
                    Actor = actor,
                    At = clock.UtcNow,
                    Read = ReadingComparer.Compare(snapshot.Extracted, booked, filledBeforeReview: shown),
                    Shown = ReadingComparer.Compare(shown, booked),
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Measuring the reading of extraction {ExtractionId} failed; the booking stands", snapshot.ExtractionId);
        }
    }
}
