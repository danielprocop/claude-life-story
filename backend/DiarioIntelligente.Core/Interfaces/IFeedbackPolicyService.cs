using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface IFeedbackPolicyService
{
    Task<int> GetCurrentPolicyVersionAsync(CancellationToken cancellationToken = default);
    Task<FeedbackPolicyRuleset> GetRulesetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PolicySummaryResponse> GetPolicySummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task InvalidateAsync(Guid? userId = null);
}

