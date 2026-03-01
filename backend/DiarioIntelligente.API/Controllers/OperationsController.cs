using DiarioIntelligente.API.Services;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OperationsController : AuthenticatedController
{
    private readonly AppDbContext _db;
    private readonly ISearchProjectionService _searchProjectionService;
    private readonly ISearchDiagnosticsService _searchDiagnosticsService;
    private readonly UserMemoryRebuildQueue _rebuildQueue;
    private readonly IEntityNormalizationService _entityNormalizationService;
    private readonly ICognitiveGraphService _cognitiveGraphService;

    public OperationsController(
        AppDbContext db,
        ISearchProjectionService searchProjectionService,
        ISearchDiagnosticsService searchDiagnosticsService,
        UserMemoryRebuildQueue rebuildQueue,
        IEntityNormalizationService entityNormalizationService,
        ICognitiveGraphService cognitiveGraphService)
    {
        _db = db;
        _searchProjectionService = searchProjectionService;
        _searchDiagnosticsService = searchDiagnosticsService;
        _rebuildQueue = rebuildQueue;
        _entityNormalizationService = entityNormalizationService;
        _cognitiveGraphService = cognitiveGraphService;
    }

    [HttpPost("reindex/entities")]
    public async Task<ActionResult> ReindexEntities()
    {
        var userId = GetUserId();
        var entities = await _db.CanonicalEntities
            .Where(item => item.UserId == userId)
            .Include(item => item.Aliases)
            .Include(item => item.Evidence)
            .ToListAsync(HttpContext.RequestAborted);

        await _searchProjectionService.ResetUserAsync(userId, HttpContext.RequestAborted);
        foreach (var entity in entities)
            await _searchProjectionService.ProjectEntityAsync(entity, HttpContext.RequestAborted);

        return Ok(new { reindexed = entities.Count });
    }

    [HttpPost("rebuild/memory")]
    public async Task<ActionResult> RebuildMemory()
    {
        var userId = GetUserId();
        await _rebuildQueue.EnqueueAsync(userId, HttpContext.RequestAborted);
        return Accepted(new { queued = true, userId });
    }

    [HttpPost("normalize/entities")]
    public async Task<ActionResult<NormalizeEntitiesResponse>> NormalizeEntities()
    {
        var response = await _entityNormalizationService.NormalizeUserEntitiesAsync(GetUserId(), HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("search/health")]
    public async Task<ActionResult<SearchHealthResponse>> SearchHealth()
    {
        var response = await _searchDiagnosticsService.GetHealthAsync(HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpPost("search/bootstrap")]
    public async Task<ActionResult<SearchBootstrapResponse>> BootstrapSearchIndices()
    {
        var response = await _searchDiagnosticsService.BootstrapIndicesAsync(HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpPost("cleanup/legacy-feedback-policies")]
    public async Task<ActionResult<LegacyFeedbackCleanupResponse>> CleanupLegacyFeedbackPolicies()
    {
        var userId = GetUserId();
        var legacyKeys = new[] { "entity_kind_override", "entity_feedback_note" };

        var stalePolicies = await _db.PersonalPolicies
            .Where(x => x.UserId == userId && legacyKeys.Contains(x.PolicyKey))
            .ToListAsync(HttpContext.RequestAborted);

        if (stalePolicies.Count > 0)
        {
            _db.PersonalPolicies.RemoveRange(stalePolicies);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
        }

        return Ok(new LegacyFeedbackCleanupResponse(stalePolicies.Count));
    }

    [HttpPost("reset/me")]
    public async Task<ActionResult> ResetMyData([FromQuery] bool includeFeedback = true)
    {
        var userId = GetUserId();
        var ct = HttpContext.RequestAborted;

        // Full user reset for iterative tuning: wipe life data + optional user-scoped feedback artifacts.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _cognitiveGraphService.ClearUserGraphAsync(userId, ct);

        var deletedChatMessages = await _db.ChatMessages.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedGoalItems = await _db.GoalItems.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedInsights = await _db.Insights.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedEnergyLogs = await _db.EnergyLogs.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedClarificationQuestions = await _db.ClarificationQuestions.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedPersonalPolicies = await _db.PersonalPolicies.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedEntryProcessingStates = await _db.EntryProcessingStates.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

        // Legacy concept graph (if still present for this user)
        var conceptIds = await _db.Concepts
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        // Entries are the primary source; deleting them cascades to processing state and join tables.
        var deletedEntries = await _db.Entries.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

        var deletedConnections = 0;
        if (conceptIds.Count > 0)
        {
            deletedConnections = await _db.Connections
                .Where(x => conceptIds.Contains(x.ConceptAId) || conceptIds.Contains(x.ConceptBId))
                .ExecuteDeleteAsync(ct);
        }

        var deletedConcepts = await _db.Concepts.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

        var deletedFeedbackCases = 0;
        var deletedFeedbackActions = 0;
        var deletedFeedbackReplayJobs = 0;
        var deletedEntityRedirects = 0;
        var deletedPolicyVersions = 0;

        if (includeFeedback)
        {
            var createdCaseIds = await _db.FeedbackCases
                .Where(x => x.CreatedByUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(ct);

            var createdActionIds = createdCaseIds.Count == 0
                ? new List<Guid>()
                : await _db.FeedbackActions
                    .Where(x => createdCaseIds.Contains(x.CaseId))
                    .Select(x => x.Id)
                    .ToListAsync(ct);

            var targetedActionIds = await _db.FeedbackActions
                .Where(x => x.TargetUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(ct);

            var actionIdsNeedingRedirectCleanup = createdActionIds
                .Concat(targetedActionIds)
                .Distinct()
                .ToList();

            if (actionIdsNeedingRedirectCleanup.Count > 0)
            {
                deletedEntityRedirects = await _db.EntityRedirects
                    .Where(x => actionIdsNeedingRedirectCleanup.Contains(x.CreatedByActionId))
                    .ExecuteDeleteAsync(ct);
            }

            // Remove user-targeted actions regardless of who created the case.
            deletedFeedbackActions += await _db.FeedbackActions
                .Where(x => x.TargetUserId == userId)
                .ExecuteDeleteAsync(ct);

            if (createdCaseIds.Count > 0)
            {
                // Remaining actions in these cases are removed by cascade.
                deletedFeedbackCases = await _db.FeedbackCases
                    .Where(x => createdCaseIds.Contains(x.Id))
                    .ExecuteDeleteAsync(ct);
            }

            deletedFeedbackReplayJobs = await _db.FeedbackReplayJobs
                .Where(x => x.TargetUserId == userId)
                .ExecuteDeleteAsync(ct);

            deletedPolicyVersions = await _db.PolicyVersions
                .Where(x => x.CreatedByUserId == userId)
                .ExecuteDeleteAsync(ct);
        }

        await transaction.CommitAsync(ct);

        await _searchProjectionService.ResetUserAsync(userId, ct);

        return Ok(new
        {
            userId,
            deletedEntries,
            deletedChatMessages,
            deletedGoalItems,
            deletedInsights,
            deletedEnergyLogs,
            deletedClarificationQuestions,
            deletedPersonalPolicies,
            deletedEntryProcessingStates,
            deletedConnections,
            deletedConcepts,
            includeFeedback,
            deletedFeedbackCases,
            deletedFeedbackActions,
            deletedFeedbackReplayJobs,
            deletedEntityRedirects,
            deletedPolicyVersions
        });
    }
}
