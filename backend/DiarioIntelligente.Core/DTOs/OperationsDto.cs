namespace DiarioIntelligente.Core.DTOs;

public record NormalizeEntitiesResponse(
    int Normalized,
    int Merged,
    int Suppressed,
    int Ambiguous,
    int Reindexed
);
