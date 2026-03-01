using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/admin/policy")]
public sealed class AdminPolicyController : AdminAuthenticatedController
{
    private readonly IFeedbackPolicyService _feedbackPolicyService;

    public AdminPolicyController(IFeedbackPolicyService feedbackPolicyService)
    {
        _feedbackPolicyService = feedbackPolicyService;
    }

    [HttpGet("version")]
    public async Task<ActionResult<PolicyVersionResponse>> GetVersion()
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var version = await _feedbackPolicyService.GetCurrentPolicyVersionAsync(HttpContext.RequestAborted);
        return Ok(new PolicyVersionResponse(version));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<PolicySummaryResponse>> GetSummary([FromQuery] Guid? userId = null)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var resolvedUserId = userId ?? GetUserId();
        var response = await _feedbackPolicyService.GetPolicySummaryAsync(resolvedUserId, HttpContext.RequestAborted);
        return Ok(response);
    }
}
