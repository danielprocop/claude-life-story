namespace DiarioIntelligente.Core.DTOs;

public record CreateEntryRequest(string Content);

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

public record PaginatedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
