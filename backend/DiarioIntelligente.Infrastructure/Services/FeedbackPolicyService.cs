using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class FeedbackPolicyService : IFeedbackPolicyService
{
    private readonly AppDbContext _db;
    private readonly ConcurrentDictionary<string, FeedbackPolicyRuleset> _rulesetCache = new();

    public FeedbackPolicyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetCurrentPolicyVersionAsync(CancellationToken cancellationToken = default)
    {
        var latestVersion = await _db.PolicyVersions
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken)
            ?? 0;

        if (latestVersion > 0)
            return latestVersion;

        return await _db.FeedbackActions
            .Where(x => x.Status == "ACTIVE")
            .Select(x => (int?)x.PolicyVersion)
            .MaxAsync(cancellationToken)
            ?? 0;
    }

    public async Task<FeedbackPolicyRuleset> GetRulesetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var policyVersion = await GetCurrentPolicyVersionAsync(cancellationToken);
        var cacheKey = BuildCacheKey(userId, policyVersion);
        if (_rulesetCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var ruleset = FeedbackPolicyRuleset.Empty(userId, policyVersion);

        var actions = await _db.FeedbackActions
            .Where(x =>
                x.Status == "ACTIVE" &&
                (x.Scope == "GLOBAL" || (x.Scope == "USER" && x.TargetUserId == userId)))
            .OrderBy(x => x.PolicyVersion)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var action in actions)
            ApplyActionToRuleset(ruleset, action);

        var redirects = await _db.EntityRedirects
            .Where(x => x.Active && x.OldEntity.UserId == userId && x.CanonicalEntity.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var redirect in redirects)
            ruleset.Redirects[redirect.OldEntityId] = redirect.CanonicalEntityId;

        _rulesetCache[cacheKey] = ruleset;
        return ruleset;
    }

    public async Task<PolicySummaryResponse> GetPolicySummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var ruleset = await GetRulesetAsync(userId, cancellationToken);
        var globalActions = await _db.FeedbackActions
            .Where(x => x.Status == "ACTIVE" && x.Scope == "GLOBAL")
            .CountAsync(cancellationToken);
        var userActions = await _db.FeedbackActions
            .Where(x => x.Status == "ACTIVE" && x.Scope == "USER" && x.TargetUserId == userId)
            .CountAsync(cancellationToken);

        return new PolicySummaryResponse(
            ruleset.PolicyVersion,
            globalActions,
            userActions,
            ruleset.BlockedTokensAny.Count + ruleset.BlockedTokensGoal.Count + ruleset.BlockedTokensPerson.Count,
            ruleset.TokenTypeOverrides.Count,
            ruleset.ForceLinkRules.Count,
            ruleset.UserAliasMap.Count,
            ruleset.EntityTypeOverrides.Count,
            ruleset.Redirects.Count);
    }

    public Task InvalidateAsync(Guid? userId = null)
    {
        if (userId == null)
        {
            _rulesetCache.Clear();
            return Task.CompletedTask;
        }

        var keys = _rulesetCache.Keys
            .Where(x => x.StartsWith($"{userId:D}:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keys)
            _rulesetCache.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    private static string BuildCacheKey(Guid userId, int policyVersion) => $"{userId:D}:{policyVersion}";

    private static void ApplyActionToRuleset(FeedbackPolicyRuleset ruleset, Core.Models.FeedbackAction action)
    {
        using var doc = JsonDocument.Parse(action.PayloadJson);
        var root = doc.RootElement;

        switch (action.ActionType)
        {
            case "BLOCK_TOKEN_GLOBAL":
            {
                if (!TryReadNormalizedToken(root, out var token))
                    return;

                var appliesTo = ReadString(root, "applies_to", "ANY");
                switch (appliesTo.ToUpperInvariant())
                {
                    case "PERSON":
                        ruleset.BlockedTokensPerson.Add(token);
                        break;
                    case "GOAL":
                        ruleset.BlockedTokensGoal.Add(token);
                        break;
                    default:
                        ruleset.BlockedTokensAny.Add(token);
                        break;
                }

                return;
            }
            case "TOKEN_TYPE_OVERRIDE_GLOBAL":
            {
                if (!TryReadNormalizedToken(root, out var token))
                    return;

                var forcedType = ReadString(root, "forced_type", string.Empty);
                if (!string.IsNullOrWhiteSpace(forcedType))
                    ruleset.TokenTypeOverrides[token] = forcedType.ToUpperInvariant();
                return;
            }
            case "ADD_ALIAS":
            {
                var normalizedAlias = ReadString(root, "alias_normalized", string.Empty);
                if (!Guid.TryParse(ReadString(root, "entity_id", string.Empty), out var entityId))
                    return;

                if (string.IsNullOrWhiteSpace(normalizedAlias))
                    return;

                ruleset.UserAliasMap[normalizedAlias] = ruleset.ResolveCanonical(entityId);
                return;
            }
            case "REMOVE_ALIAS":
            {
                var normalizedAlias = ReadString(root, "alias_normalized", string.Empty);
                if (!string.IsNullOrWhiteSpace(normalizedAlias))
                    ruleset.UserAliasMap.Remove(normalizedAlias);
                return;
            }
            case "FORCE_LINK_RULE":
            {
                if (!Guid.TryParse(ReadString(root, "entity_id", string.Empty), out var entityId))
                    return;

                var patternValue = ReadString(root, "pattern_value", string.Empty);
                if (string.IsNullOrWhiteSpace(patternValue))
                    return;

                List<string>? nearTokens = null;
                if (root.TryGetProperty("constraints", out var constraints) &&
                    constraints.ValueKind == JsonValueKind.Object &&
                    constraints.TryGetProperty("near_tokens", out var nearTokensElement) &&
                    nearTokensElement.ValueKind == JsonValueKind.Array)
                {
                    nearTokens = nearTokensElement
                        .EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString() ?? string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();
                }

                int? withinChars = null;
                if (root.TryGetProperty("constraints", out var constraintsRoot) &&
                    constraintsRoot.ValueKind == JsonValueKind.Object &&
                    constraintsRoot.TryGetProperty("within_chars", out var withinCharsElement) &&
                    withinCharsElement.TryGetInt32(out var parsedWithinChars))
                {
                    withinChars = parsedWithinChars;
                }

                var language = root.TryGetProperty("constraints", out var constraintsNode) &&
                               constraintsNode.ValueKind == JsonValueKind.Object &&
                               constraintsNode.TryGetProperty("language", out var languageElement) &&
                               languageElement.ValueKind == JsonValueKind.String
                    ? languageElement.GetString()
                    : null;

                ruleset.ForceLinkRules.Add(new FeedbackForceLinkRule(
                    ReadString(root, "pattern_kind", "EXACT"),
                    patternValue,
                    ruleset.ResolveCanonical(entityId),
                    language,
                    nearTokens,
                    withinChars));
                return;
            }
            case "ENTITY_TYPE_CORRECTION":
            {
                if (!Guid.TryParse(ReadString(root, "entity_id", string.Empty), out var entityId))
                    return;

                var newType = ReadString(root, "new_type", string.Empty);
                if (!string.IsNullOrWhiteSpace(newType))
                    ruleset.EntityTypeOverrides[ruleset.ResolveCanonical(entityId)] = newType.ToLowerInvariant();
                return;
            }
            case "PATTERN_RULE_GLOBAL":
            {
                var patternName = ReadString(root, "pattern_name", string.Empty);
                if (string.IsNullOrWhiteSpace(patternName))
                    return;

                ruleset.PatternRules.Add(new FeedbackPatternRule(
                    patternName,
                    ReadString(root, "pattern_spec", string.Empty),
                    ReadString(root, "transform", string.Empty)));
                return;
            }
        }
    }

    private static bool TryReadNormalizedToken(JsonElement root, out string token)
    {
        token = ReadString(root, "token_normalized", string.Empty);
        if (string.IsNullOrWhiteSpace(token))
            token = Normalize(ReadString(root, "token", string.Empty));
        return !string.IsNullOrWhiteSpace(token);
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? fallback;
        return fallback;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized = raw.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}

