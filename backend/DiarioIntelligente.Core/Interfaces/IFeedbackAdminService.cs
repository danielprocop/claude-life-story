using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface IFeedbackAdminService
{
    Task<FeedbackPreviewResponse> PreviewCaseAsync(
        Guid actorUserId,
        string actorRole,
        FeedbackCasePreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<FeedbackApplyResponse> ApplyCaseAsync(
        Guid actorUserId,
        string actorRole,
        FeedbackCaseApplyRequest request,
        CancellationToken cancellationToken = default);

    Task<List<FeedbackCaseSummaryResponse>> GetCasesAsync(
        string? status,
        string? templateId,
        int take,
        CancellationToken cancellationToken = default);

    Task<FeedbackCaseDetailResponse?> GetCaseAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<RevertFeedbackCaseResponse?> RevertCaseAsync(
        Guid actorUserId,
        string actorRole,
        Guid caseId,
        CancellationToken cancellationToken = default);

    Task<List<FeedbackReviewQueueItemResponse>> GetReviewQueueAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default);

    Task<List<FeedbackReplayJobItemResponse>> GetReplayJobsAsync(
        Guid userId,
        string? status,
        int take,
        CancellationToken cancellationToken = default);

    Task<List<NodeSearchItemResponse>> SearchEntitiesAsync(
        Guid userId,
        string query,
        int take,
        CancellationToken cancellationToken = default);

    Task<EntityDebugResponse?> GetEntityDebugAsync(
        Guid userId,
        Guid entityId,
        CancellationToken cancellationToken = default);

    Task<FeedbackAssistResponse> AssistTemplateAsync(
        Guid userId,
        string text);
}
