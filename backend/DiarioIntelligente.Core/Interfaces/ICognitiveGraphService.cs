using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface ICognitiveGraphService
{
    Task ProcessEntryAsync(
        Entry entry,
        AiAnalysisResult analysis,
        FeedbackPolicyRuleset? feedbackRuleset = null,
        CancellationToken cancellationToken = default);
    Task ClearUserGraphAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NodeSearchResponse> SearchNodesAsync(Guid userId, string? query, int limit = 24, CancellationToken cancellationToken = default);
    Task<NodeViewResponse?> GetNodeViewAsync(Guid userId, Guid entityId, CancellationToken cancellationToken = default);
}
