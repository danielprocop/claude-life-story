namespace DiarioIntelligente.Core.DTOs;

public record PersonalModelResponse(
    DateTime GeneratedAt,
    int EntriesAnalyzed,
    int CanonicalEntities,
    int ActiveGoals,
    string ContextSummary,
    List<ProfileSignalResponse> PersonalitySignals,
    List<string> PhilosophicalThemes,
    List<string> CurrentFocus,
    List<string> SuggestedMicroSteps,
    List<string> AdaptationRules
);

public record ProfileSignalResponse(
    string Trait,
    int Score,
    string Rationale
);
