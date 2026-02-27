namespace DiarioIntelligente.Core.DTOs;

public record ConceptResponse(
    Guid Id,
    string Label,
    string Type,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    int EntryCount
);
