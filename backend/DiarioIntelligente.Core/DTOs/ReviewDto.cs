namespace DiarioIntelligente.Core.DTOs;

public record ReviewResponse(
    string Summary,
    string Period,
    List<string> KeyThemes,
    List<string> Accomplishments,
    List<string> Challenges,
    List<string> Patterns,
    List<string> Suggestions,
    DateTime GeneratedAt
);
