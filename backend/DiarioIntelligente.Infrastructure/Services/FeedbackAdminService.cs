using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class FeedbackAdminService : IFeedbackAdminService
{
    private static readonly HashSet<string> StopwordCandidates = new(StringComparer.OrdinalIgnoreCase)
    {
        "inoltre",
        "quindi",
        "pero",
        "però",
        "dunque",
        "infatti",
        "comunque"
    };
    private static readonly HashSet<string> MergeSafeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "person",
        "goal",
        "project",
        "activity",
        "idea",
        "problem",
        "emotion",
        "object"
    };

    private readonly AppDbContext _db;
    private readonly IFeedbackPolicyService _policyService;
    private readonly ISearchProjectionService _searchProjectionService;
    private readonly ICognitiveGraphService _cognitiveGraphService;
    private readonly IFeedbackReplayScheduler _replayQueue;
    private readonly ILogger<FeedbackAdminService> _logger;

    public FeedbackAdminService(
        AppDbContext db,
        IFeedbackPolicyService policyService,
        ISearchProjectionService searchProjectionService,
        ICognitiveGraphService cognitiveGraphService,
        IFeedbackReplayScheduler replayQueue,
        ILogger<FeedbackAdminService> logger)
    {
        _db = db;
        _policyService = policyService;
        _searchProjectionService = searchProjectionService;
        _cognitiveGraphService = cognitiveGraphService;
        _replayQueue = replayQueue;
        _logger = logger;
    }

    public Task<FeedbackPreviewResponse> PreviewCaseAsync(
        Guid actorUserId,
        string actorRole,
        FeedbackCasePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        return PreviewCaseInternalAsync(actorUserId, request, cancellationToken);
    }

    public Task<FeedbackApplyResponse> ApplyCaseAsync(
        Guid actorUserId,
        string actorRole,
        FeedbackCaseApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        return ApplyCaseInternalAsync(actorUserId, actorRole, request, cancellationToken);
    }

    public Task<List<FeedbackCaseSummaryResponse>> GetCasesAsync(
        string? status,
        string? templateId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return GetCasesInternalAsync(status, templateId, take, cancellationToken);
    }

    public Task<FeedbackCaseDetailResponse?> GetCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        return GetCaseInternalAsync(caseId, cancellationToken);
    }

    public Task<RevertFeedbackCaseResponse?> RevertCaseAsync(
        Guid actorUserId,
        string actorRole,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        return RevertCaseInternalAsync(actorUserId, caseId, cancellationToken);
    }

    public Task<List<FeedbackReviewQueueItemResponse>> GetReviewQueueAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return GetReviewQueueInternalAsync(userId, take, cancellationToken);
    }

    public Task<List<FeedbackReplayJobItemResponse>> GetReplayJobsAsync(
        Guid userId,
        string? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        return GetReplayJobsInternalAsync(userId, status, take, cancellationToken);
    }

    public Task<List<NodeSearchItemResponse>> SearchEntitiesAsync(
        Guid userId,
        string query,
        int take,
        CancellationToken cancellationToken = default)
    {
        return SearchEntitiesInternalAsync(userId, query, take, cancellationToken);
    }

    public Task<EntityDebugResponse?> GetEntityDebugAsync(
        Guid userId,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return GetEntityDebugInternalAsync(userId, entityId, cancellationToken);
    }

    public Task<FeedbackAssistResponse> AssistTemplateAsync(Guid userId, string text)
    {
        var lowered = text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lowered))
            return Task.FromResult(new FeedbackAssistResponse("T1", "{}", 0, "Nessun testo fornito."));

        if (lowered.Contains("blocca") || lowered.Contains("stopword"))
        {
            var token = ExtractQuotedToken(text) ?? "inoltre";
            var payload = JsonSerializer.Serialize(new
            {
                token,
                applies_to = "PERSON",
                classification = "CONNECTIVE"
            });
            return Task.FromResult(new FeedbackAssistResponse("T1", payload, 0.76, "Pattern rilevato: richiesta blocco token."));
        }

        if (lowered.Contains("merge") || lowered.Contains("duplica"))
        {
            var payload = JsonSerializer.Serialize(new
            {
                entity_a_id = Guid.Empty,
                entity_b_id = Guid.Empty,
                canonical_id = Guid.Empty,
                migrate_alias = true,
                migrate_edges = true,
                migrate_evidence = true,
                reason = "assist_merge_placeholder"
            });
            return Task.FromResult(new FeedbackAssistResponse("T3", payload, 0.55, "Pattern rilevato: richiesta merge entita."));
        }

        return Task.FromResult(new FeedbackAssistResponse("T2", JsonSerializer.Serialize(new
        {
            token = "inoltre",
            forced_type = "CONNECTIVE"
        }), 0.42, "Suggerimento fallback: override tipo token."));
    }

    private async Task<FeedbackPreviewResponse> PreviewCaseInternalAsync(
        Guid actorUserId,
        FeedbackCasePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var compilation = await CompileTemplateAsync(actorUserId, request.TemplateId, request.TemplatePayload, request.TargetUserId, request.ScopeDefault, cancellationToken);
        var impact = await AnalyzeImpactAsync(compilation.Actions, request.TargetUserId ?? actorUserId, cancellationToken);

        return new FeedbackPreviewResponse(
            compilation.Actions.Select(MapParsedAction).ToList(),
            MapImpact(impact),
            compilation.Warnings,
            impact.ImpactedEntities > 0 || impact.EntriesToReplay > 0);
    }

    private async Task<FeedbackApplyResponse> ApplyCaseInternalAsync(
        Guid actorUserId,
        string actorRole,
        FeedbackCaseApplyRequest request,
        CancellationToken cancellationToken)
    {
        var compilation = await CompileTemplateAsync(actorUserId, request.TemplateId, request.TemplatePayload, request.TargetUserId, request.ScopeDefault, cancellationToken);
        var impact = await AnalyzeImpactAsync(compilation.Actions, request.TargetUserId ?? actorUserId, cancellationToken);

        var nextPolicyVersion = (await _db.PolicyVersions.MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;

        var feedbackCase = new FeedbackCase
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CreatedByUserId = actorUserId,
            CreatedByRole = NormalizeRole(actorRole),
            ScopeDefault = ResolveCaseScope(compilation.Actions),
            Status = "APPLIED",
            TemplateId = request.TemplateId.Trim().ToUpperInvariant(),
            TemplatePayloadJson = request.TemplatePayload.GetRawText(),
            ReferencesJson = request.References?.GetRawText(),
            PreviewSummaryJson = JsonSerializer.Serialize(impact),
            AppliedPolicyVersion = nextPolicyVersion,
            Reason = request.Reason
        };

        var summaryJson = JsonSerializer.Serialize(new
        {
            template = feedbackCase.TemplateId,
            actions = compilation.Actions.Count,
            created_by = actorUserId
        });

        _db.PolicyVersions.Add(new PolicyVersion
        {
            Version = nextPolicyVersion,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
            SummaryJson = summaryJson,
            Fingerprint = ComputeFingerprint(summaryJson)
        });

        _db.FeedbackCases.Add(feedbackCase);

        var actionEntities = compilation.Actions
            .Select(item => new FeedbackAction
            {
                Id = Guid.NewGuid(),
                CaseId = feedbackCase.Id,
                CreatedAt = now,
                Scope = item.Scope,
                TargetUserId = item.TargetUserId,
                ActionType = item.ActionType,
                PayloadJson = item.PayloadJson,
                Status = "ACTIVE",
                PolicyVersion = nextPolicyVersion
            })
            .ToList();

        _db.FeedbackActions.AddRange(actionEntities);
        var replayUserId = DetermineReplayUserId(actionEntities, request.TargetUserId, actorUserId);

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var action in actionEntities)
            await ApplyActionSideEffectsAsync(action, replayUserId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _policyService.InvalidateAsync();

        var replayJob = await CreateReplayJobAsync(nextPolicyVersion, replayUserId, impact, dryRun: false, cancellationToken);

        _logger.LogInformation(
            "Feedback case {CaseId} applied with policy version {PolicyVersion}. Actions={ActionsCount}.",
            feedbackCase.Id,
            nextPolicyVersion,
            actionEntities.Count);

        return new FeedbackApplyResponse(
            feedbackCase.Id,
            nextPolicyVersion,
            actionEntities.Select(MapParsedAction).ToList(),
            replayJob);
    }

    private async Task<List<FeedbackCaseSummaryResponse>> GetCasesInternalAsync(
        string? status,
        string? templateId,
        int take,
        CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        IQueryable<FeedbackCase> query = _db.FeedbackCases;

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            var normalizedTemplate = templateId.Trim().ToUpperInvariant();
            query = query.Where(x => x.TemplateId == normalizedTemplate);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .Select(x => new FeedbackCaseSummaryResponse(
                x.Id,
                x.CreatedAt,
                x.Status,
                x.TemplateId,
                x.ScopeDefault,
                x.AppliedPolicyVersion,
                x.Reason))
            .ToListAsync(cancellationToken);
    }

    private async Task<FeedbackCaseDetailResponse?> GetCaseInternalAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var item = await _db.FeedbackCases
            .Where(x => x.Id == caseId)
            .Include(x => x.Actions)
            .FirstOrDefaultAsync(cancellationToken);

        if (item == null)
            return null;

        return new FeedbackCaseDetailResponse(
            item.Id,
            item.CreatedAt,
            item.CreatedByUserId,
            item.CreatedByRole,
            item.ScopeDefault,
            item.Status,
            item.TemplateId,
            item.TemplatePayloadJson,
            item.ReferencesJson,
            item.PreviewSummaryJson,
            item.AppliedPolicyVersion,
            item.Reason,
            item.Actions
                .OrderBy(x => x.CreatedAt)
                .Select(MapParsedAction)
                .ToList());
    }

    private async Task<RevertFeedbackCaseResponse?> RevertCaseInternalAsync(
        Guid actorUserId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var feedbackCase = await _db.FeedbackCases
            .Where(x => x.Id == caseId)
            .Include(x => x.Actions)
            .FirstOrDefaultAsync(cancellationToken);

        if (feedbackCase == null)
            return null;

        var activeActions = feedbackCase.Actions.Where(x => x.Status == "ACTIVE").ToList();
        if (activeActions.Count == 0)
            return new RevertFeedbackCaseResponse(caseId, feedbackCase.AppliedPolicyVersion ?? 0, 0, new FeedbackReplayJobResponse(Guid.Empty, "completed", false));

        var nextPolicyVersion = (await _db.PolicyVersions.MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;

        _db.PolicyVersions.Add(new PolicyVersion
        {
            Version = nextPolicyVersion,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
            SummaryJson = JsonSerializer.Serialize(new
            {
                revert_case = caseId,
                reverted_actions = activeActions.Count
            }),
            Fingerprint = null
        });

        foreach (var action in activeActions)
        {
            action.Status = "REVERTED";

            if (action.ActionType == "MERGE_ENTITIES")
            {
                var redirects = await _db.EntityRedirects
                    .Where(x => x.CreatedByActionId == action.Id && x.Active)
                    .ToListAsync(cancellationToken);

                foreach (var redirect in redirects)
                    redirect.Active = false;
            }
            else if (action.ActionType == "UNDO_MERGE")
            {
                await UndoMergeFromPayloadAsync(action.PayloadJson, cancellationToken);
            }
        }

        feedbackCase.Status = "REVERTED";
        feedbackCase.AppliedPolicyVersion = nextPolicyVersion;

        await _db.SaveChangesAsync(cancellationToken);
        await _policyService.InvalidateAsync();

        var replayUserId = DetermineReplayUserId(activeActions, feedbackCase.CreatedByUserId, feedbackCase.CreatedByUserId);
        var impact = await AnalyzeImpactAsync(activeActions.Select(x => new FeedbackCompiledAction(x.Scope, x.TargetUserId, x.ActionType, x.PayloadJson)).ToList(), replayUserId ?? feedbackCase.CreatedByUserId, cancellationToken);
        var replayJob = await CreateReplayJobAsync(nextPolicyVersion, replayUserId, impact, false, cancellationToken);

        return new RevertFeedbackCaseResponse(caseId, nextPolicyVersion, activeActions.Count, replayJob);
    }

    private async Task<List<FeedbackReviewQueueItemResponse>> GetReviewQueueInternalAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        var queue = new List<FeedbackReviewQueueItemResponse>();

        var entities = await _db.CanonicalEntities
            .Where(x => x.UserId == userId)
            .Include(x => x.Aliases)
            .Include(x => x.Evidence)
            .ToListAsync(cancellationToken);

        foreach (var person in entities.Where(x => x.Kind == "person" && StopwordCandidates.Contains(x.NormalizedCanonicalName)))
        {
            var payload = JsonSerializer.Serialize(new
            {
                token = person.CanonicalName,
                token_normalized = person.NormalizedCanonicalName,
                applies_to = "PERSON",
                classification = "CONNECTIVE"
            });

            queue.Add(new FeedbackReviewQueueItemResponse(
                "STOPWORD_AS_PERSON",
                "high",
                $"Token '{person.CanonicalName}' classificato come persona.",
                new List<Guid> { person.Id },
                person.Evidence.Select(x => x.EntryId).Distinct().Take(5).ToList(),
                person.Evidence.Select(x => x.Snippet).Take(3).ToList(),
                "T1",
                payload));
        }

        var duplicateGroups = entities
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.NormalizedCanonicalName) &&
                MergeSafeKinds.Contains(x.Kind))
            .GroupBy(x => new { x.Kind, x.NormalizedCanonicalName })
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            var candidates = group
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.AnchorKey))
                .ThenByDescending(x => x.Evidence.Count)
                .ToList();
            if (candidates.Count < 2)
                continue;

            var canonical = candidates[0];
            var duplicate = candidates[1];
            var payload = JsonSerializer.Serialize(new
            {
                entity_a_id = canonical.Id,
                entity_b_id = duplicate.Id,
                canonical_id = canonical.Id,
                migrate_alias = true,
                migrate_edges = true,
                migrate_evidence = true,
                reason = "review_queue_duplicate_name"
            });

            queue.Add(new FeedbackReviewQueueItemResponse(
                canonical.Kind == "goal" ? "DUPLICATE_GOALS" : "DUPLICATE_ENTITIES",
                "medium",
                $"Possibile duplicato '{canonical.CanonicalName}'.",
                new List<Guid> { canonical.Id, duplicate.Id },
                canonical.Evidence.Select(x => x.EntryId)
                    .Concat(duplicate.Evidence.Select(x => x.EntryId))
                    .Distinct()
                    .Take(5)
                    .ToList(),
                canonical.Evidence.Select(x => x.Snippet)
                    .Concat(duplicate.Evidence.Select(x => x.Snippet))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList(),
                "T3",
                payload));
        }

        var lowConfidence = entities
            .Where(x => x.Evidence.Count > 0 && x.Evidence.Average(e => e.Confidence) < 0.55f)
            .OrderBy(x => x.Evidence.Average(e => e.Confidence))
            .Take(20)
            .ToList();

        foreach (var entity in lowConfidence)
        {
            var payload = JsonSerializer.Serialize(new
            {
                entity_id = entity.Id,
                new_type = entity.Kind,
                reason = "low_confidence_needs_review"
            });

            queue.Add(new FeedbackReviewQueueItemResponse(
                "LOW_CONFIDENCE",
                "low",
                $"Nodo '{entity.CanonicalName}' con confidenza media bassa.",
                new List<Guid> { entity.Id },
                entity.Evidence.Select(x => x.EntryId).Distinct().Take(3).ToList(),
                entity.Evidence.Select(x => x.Snippet).Take(3).ToList(),
                "T4",
                payload));
        }

        return queue
            .OrderByDescending(x => x.Severity == "high")
            .ThenByDescending(x => x.Severity == "medium")
            .Take(safeTake)
            .ToList();
    }

    private async Task<List<NodeSearchItemResponse>> SearchEntitiesInternalAsync(
        Guid userId,
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        var result = await _cognitiveGraphService.SearchNodesAsync(userId, query, take, cancellationToken);
        return result.Items;
    }

    private async Task<List<FeedbackReplayJobItemResponse>> GetReplayJobsInternalAsync(
        Guid userId,
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToLowerInvariant();

        IQueryable<FeedbackReplayJob> query = _db.FeedbackReplayJobs
            .Where(x => x.TargetUserId == null || x.TargetUserId == userId);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
            query = query.Where(x => x.Status == normalizedStatus);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .Select(x => new FeedbackReplayJobItemResponse(
                x.Id,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt,
                x.Status,
                x.PolicyVersion,
                x.TargetUserId,
                x.DryRun,
                x.SummaryJson,
                x.Error))
            .ToListAsync(cancellationToken);
    }

    private async Task<EntityDebugResponse?> GetEntityDebugInternalAsync(
        Guid userId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var entity = await _db.CanonicalEntities
            .Where(x => x.UserId == userId && x.Id == entityId)
            .Include(x => x.Aliases)
            .Include(x => x.Evidence)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
            return null;

        var ruleset = await _policyService.GetRulesetAsync(userId, cancellationToken);
        var canonicalId = ruleset.ResolveCanonical(entityId);
        var chain = await BuildRedirectChainAsync(userId, entityId, cancellationToken);

        var candidateActions = await _db.FeedbackActions
            .Where(x =>
                x.Scope == "GLOBAL" || (x.Scope == "USER" && x.TargetUserId == userId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var entityIdToken = entityId.ToString();
        var relevantActions = candidateActions
            .Where(x =>
                x.PayloadJson.Contains(entityIdToken, StringComparison.OrdinalIgnoreCase) ||
                x.PayloadJson.Contains(entity.NormalizedCanonicalName, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();

        var why = new List<string>();
        if (canonicalId != entityId)
            why.Add($"Redirect attivo verso nodo canonico {canonicalId}.");

        if (ruleset.EntityTypeOverrides.TryGetValue(canonicalId, out var overriddenType))
            why.Add($"Tipo forzato da feedback: {overriddenType}.");

        if (ruleset.UserAliasMap.ContainsValue(canonicalId))
            why.Add("Alias map utente attiva su questo nodo.");

        if (why.Count == 0)
            why.Add("Nessuna regola esplicita attiva: nodo derivato da estrazione standard.");

        var resolutionState = canonicalId == entityId ? "normal" : "redirected";

        return new EntityDebugResponse(
            entityId,
            canonicalId,
            chain,
            entity.Kind,
            entity.CanonicalName,
            entity.Aliases.Select(x => x.Alias).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            resolutionState,
            why,
            relevantActions.Select(MapParsedAction).ToList(),
            entity.Evidence
                .OrderByDescending(x => x.RecordedAt)
                .Take(20)
                .Select(x => $"{x.RecordedAt:O} [{x.EvidenceType}] {x.Snippet}")
                .ToList());
    }

    private async Task<FeedbackTemplateCompilation> CompileTemplateAsync(
        Guid actorUserId,
        string templateId,
        JsonElement payload,
        Guid? requestTargetUserId,
        string? requestedScopeDefault,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var normalizedTemplate = templateId.Trim().ToUpperInvariant();
        var warnings = new List<string>();
        var actions = new List<FeedbackCompiledAction>();

        Guid EnsureUserScopeTarget() => requestTargetUserId ?? actorUserId;

        switch (normalizedTemplate)
        {
            case "T1":
            {
                var token = ReadStringRequired(payload, "token");
                var normalizedToken = Normalize(token);
                var appliesTo = ReadString(payload, "applies_to", "ANY").ToUpperInvariant();
                var classification = ReadString(payload, "classification", "CONNECTIVE").ToUpperInvariant();

                actions.Add(new FeedbackCompiledAction(
                    "GLOBAL",
                    null,
                    "BLOCK_TOKEN_GLOBAL",
                    JsonSerializer.Serialize(new
                    {
                        token,
                        token_normalized = normalizedToken,
                        applies_to = appliesTo,
                        classification
                    })));
                break;
            }
            case "T2":
            {
                var token = ReadStringRequired(payload, "token");
                var normalizedToken = Normalize(token);
                var forcedType = ReadStringRequired(payload, "forced_type").ToUpperInvariant();

                actions.Add(new FeedbackCompiledAction(
                    "GLOBAL",
                    null,
                    "TOKEN_TYPE_OVERRIDE_GLOBAL",
                    JsonSerializer.Serialize(new
                    {
                        token,
                        token_normalized = normalizedToken,
                        forced_type = forcedType
                    })));
                break;
            }
            case "T3":
            {
                var entityAId = ReadGuidRequired(payload, "entity_a_id");
                var entityBId = ReadGuidRequired(payload, "entity_b_id");
                var canonicalId = ReadGuid(payload, "canonical_id") ?? entityAId;
                var migrateAlias = ReadBool(payload, "migrate_alias", true);
                var migrateEdges = ReadBool(payload, "migrate_edges", true);
                var migrateEvidence = ReadBool(payload, "migrate_evidence", true);
                var reason = ReadString(payload, "reason", "manual_merge");

                var source = canonicalId == entityAId ? entityBId : entityAId;
                var target = canonicalId;
                await ValidateMergeEntitiesAsync(source, target, cancellationToken);

                actions.Add(new FeedbackCompiledAction(
                    "USER",
                    EnsureUserScopeTarget(),
                    "MERGE_ENTITIES",
                    JsonSerializer.Serialize(new
                    {
                        source_id = source,
                        target_id = target,
                        canonical_id = canonicalId,
                        migrate_alias = migrateAlias,
                        migrate_edges = migrateEdges,
                        migrate_evidence = migrateEvidence,
                        reason
                    })));
                break;
            }
            case "T4":
            {
                var entityId = ReadGuidRequired(payload, "entity_id");
                var newType = ReadStringRequired(payload, "new_type");
                var reason = ReadString(payload, "reason", "type_correction");

                actions.Add(new FeedbackCompiledAction(
                    "USER",
                    EnsureUserScopeTarget(),
                    "ENTITY_TYPE_CORRECTION",
                    JsonSerializer.Serialize(new
                    {
                        entity_id = entityId,
                        new_type = newType.ToLowerInvariant(),
                        reason
                    })));
                break;
            }
            case "T5":
            {
                var entityId = ReadGuidRequired(payload, "entity_id");
                var alias = ReadStringRequired(payload, "alias");
                var op = ReadString(payload, "op", "ADD").ToUpperInvariant();
                var normalizedAlias = Normalize(alias);

                if (op == "REMOVE")
                {
                    actions.Add(new FeedbackCompiledAction(
                        "USER",
                        EnsureUserScopeTarget(),
                        "REMOVE_ALIAS",
                        JsonSerializer.Serialize(new
                        {
                            entity_id = entityId,
                            alias_normalized = normalizedAlias
                        })));
                }
                else
                {
                    actions.Add(new FeedbackCompiledAction(
                        "USER",
                        EnsureUserScopeTarget(),
                        "ADD_ALIAS",
                        JsonSerializer.Serialize(new
                        {
                            entity_id = entityId,
                            alias_raw = alias,
                            alias_normalized = normalizedAlias
                        })));
                }
                break;
            }
            case "T6":
            {
                var entityId = ReadGuidRequired(payload, "entity_id");
                var patternKind = ReadString(payload, "pattern_kind", "EXACT").ToUpperInvariant();
                var patternValue = ReadStringRequired(payload, "pattern_value");
                var constraints = payload.TryGetProperty("constraints", out var constraintsNode) && constraintsNode.ValueKind == JsonValueKind.Object
                    ? constraintsNode.GetRawText()
                    : "{}";

                actions.Add(new FeedbackCompiledAction(
                    "USER",
                    EnsureUserScopeTarget(),
                    "FORCE_LINK_RULE",
                    JsonSerializer.Serialize(new
                    {
                        pattern_kind = patternKind,
                        pattern_value = patternValue,
                        entity_id = entityId,
                        constraints = JsonSerializer.Deserialize<Dictionary<string, object?>>(constraints)
                    })));
                break;
            }
            case "T7":
            {
                var mergeActionId = ReadGuidRequired(payload, "merge_action_id");
                actions.Add(new FeedbackCompiledAction(
                    "USER",
                    EnsureUserScopeTarget(),
                    "UNDO_MERGE",
                    JsonSerializer.Serialize(new
                    {
                        merge_action_id = mergeActionId
                    })));
                break;
            }
            case "T8":
            {
                var patternName = ReadStringRequired(payload, "pattern_name");
                var patternSpec = ReadStringRequired(payload, "pattern_spec");
                var transform = ReadString(payload, "suggested_transform", string.Empty);

                actions.Add(new FeedbackCompiledAction(
                    "GLOBAL",
                    null,
                    "PATTERN_RULE_GLOBAL",
                    JsonSerializer.Serialize(new
                    {
                        pattern_name = patternName,
                        pattern_spec = patternSpec,
                        transform
                    })));
                break;
            }
            default:
                throw new InvalidOperationException($"Template non supportato: {templateId}");
        }

        if (!string.IsNullOrWhiteSpace(requestedScopeDefault))
            warnings.Add($"Scope richiesto '{requestedScopeDefault}' ignorato: lo scope effettivo e guidato dal template.");

        return new FeedbackTemplateCompilation(actions, warnings);
    }

    private async Task<FeedbackImpactSummary> AnalyzeImpactAsync(
        List<FeedbackCompiledAction> actions,
        Guid fallbackUserId,
        CancellationToken cancellationToken)
    {
        var impactedEntityIds = new HashSet<Guid>();
        var entryIds = new HashSet<Guid>();

        foreach (var action in actions)
        {
            using var payloadDoc = JsonDocument.Parse(action.PayloadJson);
            var payload = payloadDoc.RootElement;
            var targetUserId = action.TargetUserId ?? fallbackUserId;

            switch (action.ActionType)
            {
                case "BLOCK_TOKEN_GLOBAL":
                case "TOKEN_TYPE_OVERRIDE_GLOBAL":
                {
                    var tokenNormalized = ReadString(payload, "token_normalized", string.Empty);
                    if (string.IsNullOrWhiteSpace(tokenNormalized))
                        break;

                    var matchedEntries = await _db.Entries
                        .Where(x => x.UserId == targetUserId)
                        .Where(x => x.Content.ToLower().Contains(tokenNormalized.ToLowerInvariant()))
                        .OrderByDescending(x => x.CreatedAt)
                        .Take(200)
                        .Select(x => x.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var entryId in matchedEntries)
                        entryIds.Add(entryId);

                    var matchedEntities = await _db.CanonicalEntities
                        .Where(x => x.UserId == targetUserId)
                        .Where(x =>
                            x.NormalizedCanonicalName == tokenNormalized ||
                            x.Aliases.Any(alias => alias.NormalizedAlias == tokenNormalized))
                        .Select(x => x.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var entityId in matchedEntities)
                        impactedEntityIds.Add(entityId);
                    break;
                }
                case "MERGE_ENTITIES":
                {
                    var sourceId = ReadGuid(payload, "source_id");
                    var targetId = ReadGuid(payload, "target_id");

                    if (sourceId.HasValue)
                    {
                        impactedEntityIds.Add(sourceId.Value);
                        await AddEvidenceEntryIdsAsync(sourceId.Value, entryIds, cancellationToken);
                    }

                    if (targetId.HasValue)
                    {
                        impactedEntityIds.Add(targetId.Value);
                        await AddEvidenceEntryIdsAsync(targetId.Value, entryIds, cancellationToken);
                    }
                    break;
                }
                case "ENTITY_TYPE_CORRECTION":
                case "ADD_ALIAS":
                case "REMOVE_ALIAS":
                case "FORCE_LINK_RULE":
                {
                    TryAddEntityId(payload, "entity_id", impactedEntityIds);
                    var entityId = ReadGuid(payload, "entity_id");
                    if (entityId.HasValue)
                        await AddEvidenceEntryIdsAsync(entityId.Value, entryIds, cancellationToken);

                    var token = ReadString(payload, "alias_raw", string.Empty);
                    if (string.IsNullOrWhiteSpace(token))
                        token = ReadString(payload, "pattern_value", string.Empty);

                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        var matches = await _db.Entries
                            .Where(x => x.UserId == targetUserId && x.Content.Contains(token))
                            .OrderByDescending(x => x.CreatedAt)
                            .Take(150)
                            .Select(x => x.Id)
                            .ToListAsync(cancellationToken);

                        foreach (var entryId in matches)
                            entryIds.Add(entryId);
                    }

                    break;
                }
                case "UNDO_MERGE":
                {
                    var mergeActionId = ReadGuid(payload, "merge_action_id");
                    if (mergeActionId.HasValue)
                    {
                        var redirects = await _db.EntityRedirects
                            .Where(x => x.CreatedByActionId == mergeActionId.Value)
                            .Select(x => new { x.OldEntityId, x.CanonicalEntityId })
                            .ToListAsync(cancellationToken);
                        foreach (var redirect in redirects)
                        {
                            impactedEntityIds.Add(redirect.OldEntityId);
                            impactedEntityIds.Add(redirect.CanonicalEntityId);
                        }
                    }
                    break;
                }
            }
        }

        var edgesEstimate = 0;
        var mentionEstimate = 0;
        if (impactedEntityIds.Count > 0)
        {
            edgesEstimate += await _db.EventParticipants
                .Where(x => impactedEntityIds.Contains(x.EntityId))
                .CountAsync(cancellationToken);

            edgesEstimate += await _db.Settlements
                .Where(x => impactedEntityIds.Contains(x.CounterpartyEntityId))
                .CountAsync(cancellationToken);

            mentionEstimate += await _db.EntityEvidence
                .Where(x => impactedEntityIds.Contains(x.EntityId))
                .CountAsync(cancellationToken);
        }

        mentionEstimate += entryIds.Count;

        return new FeedbackImpactSummary(
            impactedEntityIds.Count,
            mentionEstimate,
            edgesEstimate,
            entryIds.Count,
            impactedEntityIds.ToList(),
            entryIds.ToList());
    }

    private async Task ApplyActionSideEffectsAsync(FeedbackAction action, Guid? replayUserId, CancellationToken cancellationToken)
    {
        using var payloadDoc = JsonDocument.Parse(action.PayloadJson);
        var payload = payloadDoc.RootElement;

        switch (action.ActionType)
        {
            case "BLOCK_TOKEN_GLOBAL":
            {
                if (!replayUserId.HasValue)
                    break;

                var normalizedToken = ReadString(payload, "token_normalized", string.Empty);
                if (string.IsNullOrWhiteSpace(normalizedToken))
                    break;

                var impactedEntities = await _db.CanonicalEntities
                    .Where(x => x.UserId == replayUserId.Value && x.Kind == "person")
                    .Where(x =>
                        x.NormalizedCanonicalName == normalizedToken ||
                        x.Aliases.Any(alias => alias.NormalizedAlias == normalizedToken))
                    .ToListAsync(cancellationToken);

                foreach (var entity in impactedEntities)
                {
                    entity.Kind = "person_suppressed";
                    entity.Description = string.IsNullOrWhiteSpace(entity.Description)
                        ? $"blocked_token:{normalizedToken}"
                        : $"{entity.Description};blocked_token:{normalizedToken}";
                    entity.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }
            case "MERGE_ENTITIES":
            {
                var sourceId = ReadGuidRequired(payload, "source_id");
                var targetId = ReadGuidRequired(payload, "target_id");
                var migrateAlias = ReadBool(payload, "migrate_alias", true);
                var migrateEdges = ReadBool(payload, "migrate_edges", true);
                var migrateEvidence = ReadBool(payload, "migrate_evidence", true);
                await MergeEntitiesAsync(sourceId, targetId, action.Id, migrateAlias, migrateEdges, migrateEvidence, cancellationToken);
                break;
            }
            case "ENTITY_TYPE_CORRECTION":
            {
                var entityId = ReadGuidRequired(payload, "entity_id");
                var newType = ReadStringRequired(payload, "new_type");
                var entity = await _db.CanonicalEntities.FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
                if (entity != null && !string.Equals(entity.Kind, newType, StringComparison.OrdinalIgnoreCase))
                {
                    entity.Kind = newType.ToLowerInvariant();
                    entity.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }
            case "ADD_ALIAS":
            {
                // Alias enforcement is deterministic via policy ruleset.
                // Physical alias backfill is delegated to replay/rebuild jobs.
                break;
            }
            case "REMOVE_ALIAS":
            {
                var entityId = ReadGuidRequired(payload, "entity_id");
                var aliasNormalized = ReadStringRequired(payload, "alias_normalized");
                var aliases = await _db.EntityAliases
                    .Where(x => x.EntityId == entityId && x.NormalizedAlias == aliasNormalized)
                    .ToListAsync(cancellationToken);
                if (aliases.Count > 0)
                    _db.EntityAliases.RemoveRange(aliases);
                break;
            }
            case "UNDO_MERGE":
            {
                await UndoMergeFromPayloadAsync(action.PayloadJson, cancellationToken);
                break;
            }
        }
    }

    private async Task UndoMergeFromPayloadAsync(string payloadJson, CancellationToken cancellationToken)
    {
        using var payloadDoc = JsonDocument.Parse(payloadJson);
        var payload = payloadDoc.RootElement;
        var mergeActionId = ReadGuid(payload, "merge_action_id");
        if (!mergeActionId.HasValue)
            return;

        var redirects = await _db.EntityRedirects
            .Where(x => x.CreatedByActionId == mergeActionId.Value && x.Active)
            .ToListAsync(cancellationToken);

        foreach (var redirect in redirects)
            redirect.Active = false;
    }

    private async Task MergeEntitiesAsync(
        Guid sourceId,
        Guid targetId,
        Guid actionId,
        bool migrateAlias,
        bool migrateEdges,
        bool migrateEvidence,
        CancellationToken cancellationToken)
    {
        if (sourceId == targetId)
            return;

        var resolvedSourceId = await ResolveCanonicalEntityIdAsync(sourceId, cancellationToken);
        var resolvedTargetId = await ResolveCanonicalEntityIdAsync(targetId, cancellationToken);
        if (resolvedSourceId == resolvedTargetId)
            return;

        var source = await _db.CanonicalEntities
            .Where(x => x.Id == resolvedSourceId)
            .FirstOrDefaultAsync(cancellationToken);
        var target = await _db.CanonicalEntities
            .Where(x => x.Id == resolvedTargetId)
            .FirstOrDefaultAsync(cancellationToken);

        if (source == null || target == null || source.UserId != target.UserId)
            return;

        if (!MergeSafeKinds.Contains(source.Kind) || !MergeSafeKinds.Contains(target.Kind))
            return;

        if (!string.Equals(source.Kind, target.Kind, StringComparison.OrdinalIgnoreCase))
            return;

        var redirect = await _db.EntityRedirects.FirstOrDefaultAsync(x => x.OldEntityId == source.Id, cancellationToken);
        if (redirect == null)
        {
            _db.EntityRedirects.Add(new EntityRedirect
            {
                OldEntityId = source.Id,
                CanonicalEntityId = target.Id,
                CreatedByActionId = actionId,
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            redirect.CanonicalEntityId = target.Id;
            redirect.CreatedByActionId = actionId;
            redirect.Active = true;
        }
        // Migrations for aliases/evidence/edges are replayed deterministically by reprocessing.
        _ = migrateAlias;
        _ = migrateEdges;
        _ = migrateEvidence;
    }

    private async Task<FeedbackReplayJobResponse> CreateReplayJobAsync(
        int policyVersion,
        Guid? targetUserId,
        FeedbackImpactSummary impact,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var job = new FeedbackReplayJob
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = "queued",
            PolicyVersion = policyVersion,
            TargetUserId = targetUserId,
            DryRun = dryRun,
            PayloadJson = JsonSerializer.Serialize(new
            {
                userId = targetUserId,
                entityIds = impact.EntityIds,
                entryIds = impact.EntryIds
            })
        };

        _db.FeedbackReplayJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        await _replayQueue.EnqueueAsync(job.Id, cancellationToken);

        return new FeedbackReplayJobResponse(job.Id, job.Status, dryRun);
    }

    private static string ResolveCaseScope(List<FeedbackCompiledAction> actions)
    {
        if (actions.Count == 0)
            return "GLOBAL";

        return actions.All(x => x.Scope == "GLOBAL") ? "GLOBAL" : "USER";
    }

    private static Guid? DetermineReplayUserId(List<FeedbackAction> actionEntities, Guid? requestedTargetUserId, Guid actorUserId)
    {
        var userScoped = actionEntities
            .Where(x => x.Scope == "USER")
            .Select(x => x.TargetUserId)
            .FirstOrDefault(x => x.HasValue);

        if (userScoped.HasValue)
            return userScoped.Value;

        return requestedTargetUserId ?? actorUserId;
    }

    private static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "ANNOTATOR";

        var normalized = role.Trim().ToUpperInvariant();
        return normalized switch
        {
            "ADMIN" => "ADMIN",
            "DEV" => "DEV",
            "ANNOTATOR" => "ANNOTATOR",
            _ => "ANNOTATOR"
        };
    }

    private async Task<List<Guid>> BuildRedirectChainAsync(Guid userId, Guid entityId, CancellationToken cancellationToken)
    {
        var redirects = await _db.EntityRedirects
            .Where(x => x.Active && x.OldEntity.UserId == userId && x.CanonicalEntity.UserId == userId)
            .ToDictionaryAsync(x => x.OldEntityId, x => x.CanonicalEntityId, cancellationToken);

        var chain = new List<Guid> { entityId };
        var visited = new HashSet<Guid> { entityId };
        var current = entityId;

        while (redirects.TryGetValue(current, out var next) && visited.Add(next))
        {
            chain.Add(next);
            current = next;
        }

        return chain;
    }

    private async Task ValidateMergeEntitiesAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        var resolvedSourceId = await ResolveCanonicalEntityIdAsync(sourceId, cancellationToken);
        var resolvedTargetId = await ResolveCanonicalEntityIdAsync(targetId, cancellationToken);
        if (resolvedSourceId == resolvedTargetId)
            return;

        var source = await _db.CanonicalEntities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resolvedSourceId, cancellationToken);
        var target = await _db.CanonicalEntities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resolvedTargetId, cancellationToken);

        if (source == null || target == null)
            throw new InvalidOperationException("Entita merge non trovata.");

        if (source.UserId != target.UserId)
            throw new InvalidOperationException("Merge non consentito tra utenti diversi.");

        if (!MergeSafeKinds.Contains(source.Kind) || !MergeSafeKinds.Contains(target.Kind))
            throw new InvalidOperationException($"Merge non supportato per kind '{source.Kind}'/'{target.Kind}'.");

        if (!string.Equals(source.Kind, target.Kind, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Merge cross-kind non consentito: '{source.Kind}' vs '{target.Kind}'.");
    }

    private async Task<Guid> ResolveCanonicalEntityIdAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var current = entityId;

        while (visited.Add(current))
        {
            var redirect = await _db.EntityRedirects
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OldEntityId == current && x.Active, cancellationToken);

            if (redirect == null)
                break;

            current = redirect.CanonicalEntityId;
        }

        return current;
    }

    private static FeedbackParsedActionResponse MapParsedAction(FeedbackCompiledAction action)
        => new(action.Scope, action.TargetUserId, action.ActionType, action.PayloadJson);

    private static FeedbackParsedActionResponse MapParsedAction(FeedbackAction action)
        => new(action.Scope, action.TargetUserId, action.ActionType, action.PayloadJson);

    private static FeedbackImpactSummaryResponse MapImpact(FeedbackImpactSummary summary)
        => new(
            summary.ImpactedEntities,
            summary.MentionLinkChangesEstimate,
            summary.EdgesToRealignEstimate,
            summary.EntriesToReplay,
            summary.EntityIds,
            summary.EntryIds);

    private static string ComputeFingerprint(string text)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return hash[..24];
    }

    private static bool TryAddEntityId(JsonElement payload, string name, HashSet<Guid> destination)
    {
        var value = ReadGuid(payload, name);
        if (!value.HasValue)
            return false;
        destination.Add(value.Value);
        return true;
    }

    private async Task AddEvidenceEntryIdsAsync(Guid entityId, HashSet<Guid> entryIds, CancellationToken cancellationToken)
    {
        var evidenceEntryIds = await _db.EntityEvidence
            .Where(x => x.EntityId == entityId)
            .OrderByDescending(x => x.RecordedAt)
            .Select(x => x.EntryId)
            .Distinct()
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var entryId in evidenceEntryIds)
            entryIds.Add(entryId);
    }

    private static string ReadString(JsonElement payload, string name, string fallback)
    {
        if (payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? fallback;
        return fallback;
    }

    private static string ReadStringRequired(JsonElement payload, string name)
    {
        var value = ReadString(payload, name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Campo obbligatorio mancante: {name}");
        return value;
    }

    private static Guid ReadGuidRequired(JsonElement payload, string name)
    {
        var value = ReadGuid(payload, name);
        if (!value.HasValue)
            throw new InvalidOperationException($"Campo obbligatorio guid mancante: {name}");
        return value.Value;
    }

    private static Guid? ReadGuid(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String when Guid.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static bool ReadBool(JsonElement payload, string name, bool fallback)
    {
        if (!payload.TryGetProperty(name, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    private static string? ExtractQuotedToken(string text)
    {
        var match = Regex.Match(text, "\"(?<token>[^\"]+)\"");
        if (match.Success)
            return match.Groups["token"].Value;
        return null;
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
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

    private static string ToDisplayName(string value)
    {
        var cleaned = value.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return cleaned;

        var textInfo = CultureInfo.GetCultureInfo("it-IT").TextInfo;
        return textInfo.ToTitleCase(cleaned.ToLowerInvariant());
    }
}
