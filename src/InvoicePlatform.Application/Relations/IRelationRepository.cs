using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Application.Relations;

/// <summary>
/// Persistence for relations. Every method takes the organisation: there is no
/// way to ask for a relation without saying whose (CLAUDE.md, hard rule 4).
/// </summary>
public interface IRelationRepository
{
    Task<IReadOnlyList<Relation>> ListAsync(string organizationId, CancellationToken cancellationToken = default);

    Task<Relation?> GetAsync(string organizationId, string relationId, CancellationToken cancellationToken = default);

    Task SaveAsync(Relation relation, CancellationToken cancellationToken = default);
}
