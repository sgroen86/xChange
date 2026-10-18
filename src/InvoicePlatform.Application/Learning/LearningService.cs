using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Learning;

namespace InvoicePlatform.Application.Learning;

/// <summary>
/// Learning from reviewers' corrections (see CorrectionMemory for what and
/// how). Extraction asks for hints and applies what was learned; booking
/// teaches; the settings page lists and forgets. All per organisation.
/// </summary>
public sealed class LearningService(ILearningStore store, IClock clock)
{
    public Task<IReadOnlyList<LearnedCorrection>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
        => store.GetCorrectionsAsync(organizationId, cancellationToken);

    /// <summary>The reading hints for the model, from what this organisation's reviewers corrected.</summary>
    public async Task<IReadOnlyList<ReadingHint>> HintsAsync(string organizationId, CancellationToken cancellationToken = default)
        => CorrectionMemory.Hints(await store.GetCorrectionsAsync(organizationId, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Puts the remembered corrections into the model's reading, and keeps
    /// the reading itself so booking can tell what the reviewer changed.
    /// </summary>
    public async Task<AppliedCorrections> ApplyAsync(
        string organizationId,
        string extractionId,
        CanonicalInvoiceDraft extracted,
        CancellationToken cancellationToken = default)
    {
        var memory = await store.GetCorrectionsAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var applied = CorrectionMemory.Apply(extracted, memory);

        await store.SaveExtractionAsync(
            organizationId,
            new ExtractionSnapshot(extractionId, extracted, [.. applied.Applied.Select(c => c.Id)], clock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return applied;
    }

    /// <summary>
    /// Compares the booked invoice with what the model read for this upload:
    /// new corrections are remembered, applied ones the reviewer undid are
    /// forgotten. Does nothing for an unknown or already used extraction.
    /// </summary>
    public async Task LearnAsync(
        string organizationId,
        string extractionId,
        CanonicalInvoiceDraft booked,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await store.TakeExtractionAsync(organizationId, extractionId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return;
        }

        var memory = await store.GetCorrectionsAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var applied = memory.Where(c => snapshot.AppliedCorrectionIds.Contains(c.Id));
        var undone = CorrectionMemory.Undone(applied, booked).ToHashSet(StringComparer.Ordinal);

        var kept = memory.Where(c => !undone.Contains(c.Id)).ToList();
        var updated = CorrectionMemory.Merge(kept, CorrectionMemory.Learn(snapshot.Extracted, booked, clock.UtcNow));

        await store.SaveCorrectionsAsync(organizationId, updated, cancellationToken).ConfigureAwait(false);
    }

    /// <returns>False when the organisation has no such correction.</returns>
    public async Task<bool> ForgetAsync(string organizationId, string correctionId, CancellationToken cancellationToken = default)
    {
        var memory = await store.GetCorrectionsAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (memory.All(c => c.Id != correctionId))
        {
            return false;
        }

        await store.SaveCorrectionsAsync(organizationId, [.. memory.Where(c => c.Id != correctionId)], cancellationToken).ConfigureAwait(false);
        return true;
    }
}
