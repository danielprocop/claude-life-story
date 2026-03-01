using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/admin/entities")]
public sealed class AdminEntitiesController : AdminAuthenticatedController
{
    private readonly IFeedbackAdminService _feedbackAdminService;

    public AdminEntitiesController(IFeedbackAdminService feedbackAdminService)
    {
        _feedbackAdminService = feedbackAdminService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<NodeSearchItemResponse>>> Search(
        [FromQuery] string q,
        [FromQuery] Guid? userId = null,
        [FromQuery] int take = 25)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        if (string.IsNullOrWhiteSpace(q))
            return Ok(new List<NodeSearchItemResponse>());

        var resolvedUserId = userId ?? GetUserId();
        var items = await _feedbackAdminService.SearchEntitiesAsync(
            resolvedUserId,
            q,
            take,
            HttpContext.RequestAborted);

        return Ok(items);
    }

    [HttpGet("{id:guid}/debug")]
    public async Task<ActionResult<EntityDebugResponse>> GetDebug(Guid id, [FromQuery] Guid? userId = null)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var resolvedUserId = userId ?? GetUserId();
        var debug = await _feedbackAdminService.GetEntityDebugAsync(
            resolvedUserId,
            id,
            HttpContext.RequestAborted);

        if (debug == null)
            return NotFound();

        return Ok(debug);
    }
}
