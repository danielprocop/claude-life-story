using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InsightsController : ControllerBase
{
    private readonly IInsightRepository _insightRepo;

    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public InsightsController(IInsightRepository insightRepo) => _insightRepo = insightRepo;

    [HttpGet]
    public async Task<ActionResult<List<InsightResponse>>> GetAll()
    {
        var insights = await _insightRepo.GetByUserAsync(DemoUserId);

        return Ok(insights.Select(i => new InsightResponse(
            i.Id,
            i.Content,
            i.GeneratedAt,
            i.Type
        )).ToList());
    }
}
