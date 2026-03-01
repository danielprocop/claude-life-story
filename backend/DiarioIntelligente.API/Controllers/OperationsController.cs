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
    private readonly UserMemoryRebuildQueue _rebuildQueue;
    private readonly IEntityNormalizationService _entityNormalizationService;

    public OperationsController(
        AppDbContext db,
        ISearchProjectionService searchProjectionService,
        UserMemoryRebuildQueue rebuildQueue,
        IEntityNormalizationService entityNormalizationService)
    {
        _db = db;
        _searchProjectionService = searchProjectionService;
        _rebuildQueue = rebuildQueue;
        _entityNormalizationService = entityNormalizationService;
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
}
