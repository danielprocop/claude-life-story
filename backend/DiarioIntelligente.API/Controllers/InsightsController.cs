using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InsightsController : AuthenticatedController
{
    private readonly IInsightRepository _insightRepo;

    public InsightsController(IInsightRepository insightRepo) => _insightRepo = insightRepo;

    [HttpGet]
    public async Task<ActionResult<List<InsightResponse>>> GetAll()
    {
        var insights = await _insightRepo.GetByUserAsync(GetUserId());

        return Ok(insights.Select(i => new InsightResponse(
            i.Id,
            i.Content,
            i.GeneratedAt,
            i.Type
        )).ToList());
    }
}
