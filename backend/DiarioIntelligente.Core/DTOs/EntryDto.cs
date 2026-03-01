namespace DiarioIntelligente.Core.DTOs;

public record CreateEntryRequest(string Content);

public record UpdateEntryRequest(string Content);

public record EntryResponse(
    Guid Id,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<ConceptResponse>? Concepts
);

public record EntryListResponse(
    Guid Id,
    string ContentPreview,
    DateTime CreatedAt,
    int ConceptCount
);

public record RelatedEntryResponse(
    Guid Id,
    string ContentPreview,
    DateTime CreatedAt,
    int SharedConceptCount
);

public record EntryEntityFeedbackRequest(
    string Label,
    string ExpectedKind,
    string? Note
);

public record EntryEntityFeedbackResponse(
    Guid EntryId,
    string Label,
    string AppliedKind,
    bool RebuildQueued,
    string Message
);

public record PaginatedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
