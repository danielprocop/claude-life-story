namespace DiarioIntelligente.Core.DTOs;

public record InsightResponse(
    Guid Id,
    string Content,
    DateTime GeneratedAt,
    string Type
);
