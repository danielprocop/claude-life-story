using System.Text.Json;

namespace DiarioIntelligente.Core.DTOs;

public record FeedbackCasePreviewRequest(
    string TemplateId,
    JsonElement TemplatePayload,
    JsonElement? References,
    string? Reason,
    string? ScopeDefault,
    Guid? TargetUserId
);

public record FeedbackCaseApplyRequest(
    string TemplateId,
    JsonElement TemplatePayload,
    JsonElement? References,
    string? Reason,
    string? ScopeDefault,
    Guid? TargetUserId,
    bool Apply = true
);

public record FeedbackParsedActionResponse(
    string Scope,
    Guid? TargetUserId,
    string ActionType,
    string PayloadJson
);

public record FeedbackImpactSummaryResponse(
    int ImpactedEntities,
    int MentionLinkChangesEstimate,
    int EdgesToRealignEstimate,
    int EntriesToReplay,
    List<Guid> EntityIds,
    List<Guid> EntryIds
);

public record FeedbackPreviewResponse(
    List<FeedbackParsedActionResponse> ParsedActions,
    FeedbackImpactSummaryResponse ImpactSummary,
    List<string> Warnings,
    bool SuggestedApply
);

public record FeedbackReplayJobResponse(
    Guid Id,
    string Status,
    bool DryRun
);

public record FeedbackApplyResponse(
    Guid CaseId,
    int PolicyVersion,
    List<FeedbackParsedActionResponse> AppliedActions,
    FeedbackReplayJobResponse ReplayJob
);

public record FeedbackCaseSummaryResponse(
    Guid Id,
    DateTime CreatedAt,
    string Status,
    string TemplateId,
    string ScopeDefault,
    int? AppliedPolicyVersion,
    string? Reason
);

public record FeedbackCaseDetailResponse(
    Guid Id,
    DateTime CreatedAt,
    Guid CreatedByUserId,
    string CreatedByRole,
    string ScopeDefault,
    string Status,
    string TemplateId,
    string TemplatePayloadJson,
    string? ReferencesJson,
    string? PreviewSummaryJson,
    int? AppliedPolicyVersion,
    string? Reason,
    List<FeedbackParsedActionResponse> Actions
);

public record RevertFeedbackCaseResponse(
    Guid CaseId,
    int PolicyVersion,
    int RevertedActions,
    FeedbackReplayJobResponse ReplayJob
);

public record FeedbackReviewQueueItemResponse(
    string IssueType,
    string Severity,
    string Title,
    List<Guid> EntityIds,
    List<Guid> EntryIds,
    List<string> EvidenceSnippets,
    string SuggestedTemplateId,
    string SuggestedPayloadJson
);

public record PolicyVersionResponse(int PolicyVersion);

public record PolicySummaryResponse(
    int PolicyVersion,
    int GlobalActions,
    int UserActions,
    int BlockedTokens,
    int TokenTypeOverrides,
    int ForceLinkRules,
    int AliasOverrides,
    int EntityTypeOverrides,
    int Redirects
);

public record EntityDebugResponse(
    Guid EntityId,
    Guid CanonicalEntityId,
    List<Guid> RedirectChain,
    string Kind,
    string CanonicalName,
    List<string> Aliases,
    string ResolutionState,
    List<string> Why,
    List<FeedbackParsedActionResponse> RelevantActions,
    List<string> Evidence
);

public record FeedbackAssistRequest(
    string Text,
    string? ReferencesJson
);

public record FeedbackAssistResponse(
    string SuggestedTemplateId,
    string SuggestedPayloadJson,
    double Confidence,
    string RationaleShort
);
