namespace DiarioIntelligente.Core.DTOs;

public record NormalizeEntitiesResponse(
    int Normalized,
    int Merged,
    int Suppressed,
    int Ambiguous,
    int Reindexed
);

public record SearchHealthResponse(
    bool Enabled,
    string Endpoint,
    string Region,
    bool PingOk,
    bool EntityIndexExists,
    bool EntryIndexExists,
    bool GoalIndexExists,
    string? Error
);

public record SearchBootstrapResponse(
    bool Enabled,
    int CreatedIndices,
    int ExistingIndices,
    int FailedIndices,
    List<string> Messages
);

public record LegacyFeedbackCleanupResponse(
    int DeletedPolicies
);
