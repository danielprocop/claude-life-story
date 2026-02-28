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

    public OperationsController(AppDbContext db, ISearchProjectionService searchProjectionService)
    {
        _db = db;
        _searchProjectionService = searchProjectionService;
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
}
