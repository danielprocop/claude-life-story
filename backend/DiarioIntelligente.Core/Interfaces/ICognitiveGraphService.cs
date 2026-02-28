using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface ICognitiveGraphService
{
    Task ProcessEntryAsync(Entry entry, AiAnalysisResult analysis, CancellationToken cancellationToken = default);
    Task ClearUserGraphAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NodeViewResponse?> GetNodeViewAsync(Guid userId, Guid entityId, CancellationToken cancellationToken = default);
}
