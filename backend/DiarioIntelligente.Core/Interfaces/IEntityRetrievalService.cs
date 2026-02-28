namespace DiarioIntelligente.Core.Interfaces;

public interface IEntityRetrievalService
{
    Task<List<EntityRetrievalCandidate>> SearchEntityCandidatesAsync(
        Guid userId,
        string query,
        int limit = 12,
        CancellationToken cancellationToken = default);
}

public sealed record EntityRetrievalCandidate(
    Guid EntityId,
    float Score
);
