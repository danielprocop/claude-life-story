namespace DiarioIntelligente.Core.DTOs;

public sealed class FeedbackPolicyRuleset
{
    private readonly Dictionary<Guid, Guid> _redirectCache = new();

    public FeedbackPolicyRuleset(Guid userId, int policyVersion)
    {
        UserId = userId;
        PolicyVersion = policyVersion;
    }

    public Guid UserId { get; }
    public int PolicyVersion { get; }
    public HashSet<string> BlockedTokensAny { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> BlockedTokensPerson { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> BlockedTokensGoal { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> TokenTypeOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Guid> UserAliasMap { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FeedbackForceLinkRule> ForceLinkRules { get; } = new();
    public Dictionary<Guid, string> EntityTypeOverrides { get; } = new();
    public Dictionary<Guid, Guid> Redirects { get; } = new();
    public List<FeedbackPatternRule> PatternRules { get; } = new();

    public bool IsTokenBlocked(string normalizedToken, string appliesTo)
    {
        if (string.IsNullOrWhiteSpace(normalizedToken))
            return false;

        if (BlockedTokensAny.Contains(normalizedToken))
            return true;

        return appliesTo.ToUpperInvariant() switch
        {
            "PERSON" => BlockedTokensPerson.Contains(normalizedToken),
            "GOAL" => BlockedTokensGoal.Contains(normalizedToken),
            _ => false
        };
    }

    public Guid ResolveCanonical(Guid entityId)
    {
        if (_redirectCache.TryGetValue(entityId, out var cached))
            return cached;

        var visited = new HashSet<Guid>();
        var current = entityId;

        while (Redirects.TryGetValue(current, out var next) && visited.Add(current))
            current = next;

        _redirectCache[entityId] = current;
        return current;
    }

    public static FeedbackPolicyRuleset Empty(Guid userId, int policyVersion) => new(userId, policyVersion);
}

public record FeedbackForceLinkRule(
    string PatternKind,
    string PatternValue,
    Guid EntityId,
    string? Language,
    List<string>? NearTokens,
    int? WithinChars
);

public record FeedbackPatternRule(
    string PatternName,
    string PatternSpec,
    string Transform
);

public record FeedbackTemplateCompilation(
    List<FeedbackCompiledAction> Actions,
    List<string> Warnings
);

public record FeedbackCompiledAction(
    string Scope,
    Guid? TargetUserId,
    string ActionType,
    string PayloadJson
);

public record FeedbackImpactSummary(
    int ImpactedEntities,
    int MentionLinkChangesEstimate,
    int EdgesToRealignEstimate,
    int EntriesToReplay,
    List<Guid> EntityIds,
    List<Guid> EntryIds
);

