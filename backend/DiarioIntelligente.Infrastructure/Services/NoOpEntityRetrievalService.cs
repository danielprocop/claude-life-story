using DiarioIntelligente.Core.Interfaces;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class NoOpEntityRetrievalService : IEntityRetrievalService
{
    public Task<List<EntityRetrievalCandidate>> SearchEntityCandidatesAsync(
        Guid userId,
        string query,
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<EntityRetrievalCandidate>());
    }
}
