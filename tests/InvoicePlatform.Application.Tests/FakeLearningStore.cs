using InvoicePlatform.Application.Learning;
using InvoicePlatform.Domain.Learning;

namespace InvoicePlatform.Application.Tests;

/// <summary>Learning data in memory, keyed by organisation as DynamoDB keys it.</summary>
internal sealed class FakeLearningStore : ILearningStore
{
    private readonly Dictionary<(string Org, string Id), ExtractionSnapshot> _extractions = [];
    private readonly Dictionary<string, IReadOnlyList<LearnedCorrection>> _corrections = [];

    public Task SaveExtractionAsync(string organizationId, ExtractionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _extractions[(organizationId, snapshot.ExtractionId)] = snapshot;
        return Task.CompletedTask;
    }

    public Task<ExtractionSnapshot?> TakeExtractionAsync(string organizationId, string extractionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_extractions.Remove((organizationId, extractionId), out var snapshot) ? snapshot : null);

    public Task<IReadOnlyList<LearnedCorrection>> GetCorrectionsAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_corrections.GetValueOrDefault(organizationId) ?? []);

    public Task SaveCorrectionsAsync(string organizationId, IReadOnlyList<LearnedCorrection> corrections, CancellationToken cancellationToken = default)
    {
        _corrections[organizationId] = corrections;
        return Task.CompletedTask;
    }
}
