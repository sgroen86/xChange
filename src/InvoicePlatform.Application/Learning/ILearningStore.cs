using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Learning;

namespace InvoicePlatform.Application.Learning;

/// <summary>
/// What the model read for an upload, and what an organisation's reviewers
/// corrected. Tenant-owned: everything is stored and read under the
/// organisation id.
/// </summary>
public interface ILearningStore
{
    /// <summary>Kept for a limited time (30 days); booking takes it.</summary>
    Task SaveExtractionAsync(string organizationId, ExtractionSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Reads and removes the snapshot, so one upload teaches once. Null when unknown or expired.</summary>
    Task<ExtractionSnapshot?> TakeExtractionAsync(string organizationId, string extractionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearnedCorrection>> GetCorrectionsAsync(string organizationId, CancellationToken cancellationToken = default);

    Task SaveCorrectionsAsync(string organizationId, IReadOnlyList<LearnedCorrection> corrections, CancellationToken cancellationToken = default);
}

/// <param name="Extracted">The model's reading, before anything was applied or filled in.</param>
/// <param name="AppliedCorrectionIds">The learned corrections put into the draft the reviewer saw.</param>
public sealed record ExtractionSnapshot(
    string ExtractionId,
    CanonicalInvoiceDraft Extracted,
    IReadOnlyList<string> AppliedCorrectionIds,
    DateTimeOffset CreatedAt);
