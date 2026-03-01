namespace DiarioIntelligente.Core.DTOs;

public record NodeViewResponse(
    Guid Id,
    string Kind,
    string CanonicalName,
    string? AnchorKey,
    List<string> Aliases,
    List<NodeRelationResponse> Relations,
    List<NodeEvidenceResponse> Evidence,
    List<string> ResolutionNotes,
    PersonNodeViewResponse? Person,
    EventNodeViewResponse? Event
);

public record NodeSearchResponse(
    string Query,
    List<NodeSearchItemResponse> Items,
    int TotalCount,
    List<NodeKindCountResponse> KindCounts
);

public record NodeSearchItemResponse(
    Guid Id,
    string Kind,
    string CanonicalName,
    string? AnchorKey,
    List<string> Aliases,
    int EvidenceCount,
    DateTime UpdatedAt,
    string ResolutionState
);

public record NodeKindCountResponse(
    string Kind,
    int Count
);

public record NodeRelationResponse(
    string Type,
    string Target
);

public record NodeEvidenceResponse(
    Guid EntryId,
    string EvidenceType,
    string Snippet,
    DateTime RecordedAt,
    string? MergeReason
);

public record PersonNodeViewResponse(
    decimal OpenUserOwes,
    decimal OpenOwedToUser,
    List<PersonEventSummaryResponse> SharedEvents,
    List<SettlementSummaryResponse> Settlements
);

public record PersonEventSummaryResponse(
    Guid EventEntityId,
    string Title,
    string EventType,
    DateTime OccurredAt,
    decimal? EventTotal,
    decimal? MyShare
);

public record SettlementSummaryResponse(
    Guid SettlementId,
    string Direction,
    decimal OriginalAmount,
    decimal RemainingAmount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    Guid? EventEntityId,
    string? EventTitle
);

public record EventNodeViewResponse(
    string EventType,
    string Title,
    DateTime OccurredAt,
    decimal? EventTotal,
    decimal? MyShare,
    string Currency,
    bool IncludesUser,
    List<EventParticipantResponse> Participants,
    List<SettlementSummaryResponse> Settlements,
    Guid SourceEntryId
);

public record EventParticipantResponse(
    Guid EntityId,
    string CanonicalName,
    string? AnchorKey,
    string Role
);
