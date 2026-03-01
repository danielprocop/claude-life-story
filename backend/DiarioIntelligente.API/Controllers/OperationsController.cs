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
    public async Task<ActionResult> ResetMyData()
    {
        var userId = GetUserId();
        var ct = HttpContext.RequestAborted;

        // Keep feedback policy history by default. This endpoint is meant to wipe "life data"
        // (entries + derived memory), not admin/dev configuration.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _cognitiveGraphService.ClearUserGraphAsync(userId, ct);

        var deletedChatMessages = await _db.ChatMessages.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedGoalItems = await _db.GoalItems.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedInsights = await _db.Insights.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedEnergyLogs = await _db.EnergyLogs.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedClarificationQuestions = await _db.ClarificationQuestions.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        var deletedPersonalPolicies = await _db.PersonalPolicies.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

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
            deletedConnections,
            deletedConcepts
        });
    }
}
