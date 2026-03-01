using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/admin/review-queue")]
public sealed class AdminReviewQueueController : AdminAuthenticatedController
{
    private readonly IFeedbackAdminService _feedbackAdminService;

    public AdminReviewQueueController(IFeedbackAdminService feedbackAdminService)
    {
        _feedbackAdminService = feedbackAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FeedbackReviewQueueItemResponse>>> GetReviewQueue(
        [FromQuery] Guid? userId = null,
        [FromQuery] int take = 50)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var resolvedUserId = userId ?? GetUserId();
        var response = await _feedbackAdminService.GetReviewQueueAsync(
            resolvedUserId,
            take,
            HttpContext.RequestAborted);

        return Ok(response);
    }
}
