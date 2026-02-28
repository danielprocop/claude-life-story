namespace DiarioIntelligente.Core.DTOs;

public record SearchResponse(
    string Query,
    List<EntrySearchHit> Entries,
    List<ConceptSearchHit> Concepts,
    List<GoalItemSearchHit> GoalItems
);

public record EntrySearchHit(
    Guid Id,
    string Preview,
    DateTime CreatedAt,
    int ConceptCount
);

public record ConceptSearchHit(
    Guid Id,
    string Label,
    string Type,
    int EntryCount,
    DateTime LastSeenAt
);

public record GoalItemSearchHit(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateTime CreatedAt,
    int SubGoalCount
);
