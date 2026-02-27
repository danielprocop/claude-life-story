namespace DiarioIntelligente.Core.DTOs;

public record ChatRequest(string Message);

public record ChatResponse(
    string Answer,
    List<ChatSourceEntry> Sources
);

public record ChatSourceEntry(
    Guid EntryId,
    string Preview,
    DateTime Date,
    float Similarity
);

public record ChatHistoryItem(
    string Role,
    string Content,
    DateTime CreatedAt
);
