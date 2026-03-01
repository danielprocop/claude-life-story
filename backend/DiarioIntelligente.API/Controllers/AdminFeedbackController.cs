using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/admin/feedback")]
public sealed class AdminFeedbackController : AdminAuthenticatedController
{
    private readonly IFeedbackAdminService _feedbackAdminService;

    public AdminFeedbackController(IFeedbackAdminService feedbackAdminService)
    {
        _feedbackAdminService = feedbackAdminService;
    }

    [HttpPost("cases/preview")]
    public async Task<ActionResult<FeedbackPreviewResponse>> PreviewCase([FromBody] FeedbackCasePreviewRequest request)
    {
        var authorizationResult = EnsureAdminRole(out var role);
        if (authorizationResult != null)
            return authorizationResult;

        var response = await _feedbackAdminService.PreviewCaseAsync(
            GetUserId(),
            role,
            request,
            HttpContext.RequestAborted);

        return Ok(response);
    }

    [HttpPost("cases/apply")]
    public async Task<ActionResult<FeedbackApplyResponse>> ApplyCase([FromBody] FeedbackCaseApplyRequest request)
    {
        var authorizationResult = EnsureAdminRole(out var role);
        if (authorizationResult != null)
            return authorizationResult;

        var response = await _feedbackAdminService.ApplyCaseAsync(
            GetUserId(),
            role,
            request,
            HttpContext.RequestAborted);

        return Ok(response);
    }

    [HttpGet("cases")]
    public async Task<ActionResult<List<FeedbackCaseSummaryResponse>>> GetCases(
        [FromQuery] string? status,
        [FromQuery] string? templateId,
        [FromQuery] int take = 100)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var response = await _feedbackAdminService.GetCasesAsync(
            status,
            templateId,
            take,
            HttpContext.RequestAborted);

        return Ok(response);
    }

    [HttpGet("cases/{id:guid}")]
    public async Task<ActionResult<FeedbackCaseDetailResponse>> GetCase(Guid id)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var response = await _feedbackAdminService.GetCaseAsync(id, HttpContext.RequestAborted);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    [HttpPost("cases/{id:guid}/revert")]
    public async Task<ActionResult<RevertFeedbackCaseResponse>> RevertCase(Guid id)
    {
        var authorizationResult = EnsureAdminRole(out var role);
        if (authorizationResult != null)
            return authorizationResult;

        var response = await _feedbackAdminService.RevertCaseAsync(
            GetUserId(),
            role,
            id,
            HttpContext.RequestAborted);

        if (response == null)
            return NotFound();

        return Ok(response);
    }

    [HttpPost("assist")]
    public async Task<ActionResult<FeedbackAssistResponse>> Assist([FromBody] FeedbackAssistRequest request)
    {
        var authorizationResult = EnsureAdminRole(out _);
        if (authorizationResult != null)
            return authorizationResult;

        var response = await _feedbackAdminService.AssistTemplateAsync(GetUserId(), request.Text ?? string.Empty);
        return Ok(response);
    }
}
