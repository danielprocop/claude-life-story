using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : AuthenticatedController
{
    private readonly IPersonalModelService _personalModelService;
    private readonly IClarificationService _clarificationService;

    public ProfileController(
        IPersonalModelService personalModelService,
        IClarificationService clarificationService)
    {
        _personalModelService = personalModelService;
        _clarificationService = clarificationService;
    }

    [HttpGet]
    public async Task<ActionResult<PersonalModelResponse>> Get()
    {
        var profile = await _personalModelService.BuildAsync(GetUserId(), HttpContext.RequestAborted);
        return Ok(profile);
    }

    [HttpGet("questions")]
    public async Task<ActionResult<List<ClarificationQuestionResponse>>> GetQuestions([FromQuery] int limit = 5)
    {
        var questions = await _clarificationService.GetOpenQuestionsAsync(GetUserId(), limit, HttpContext.RequestAborted);
        return Ok(questions);
    }

    [HttpPost("questions/{id:guid}/answer")]
    public async Task<ActionResult> Answer(Guid id, [FromBody] AnswerClarificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Answer))
            return BadRequest(new { error = "Answer is required." });

        var updated = await _clarificationService.AnswerAsync(GetUserId(), id, request.Answer, HttpContext.RequestAborted);
        if (!updated)
            return NotFound();

        return NoContent();
    }
}
