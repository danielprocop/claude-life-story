using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoalsController : AuthenticatedController
{
    private readonly IConceptRepository _conceptRepo;

    public GoalsController(IConceptRepository conceptRepo) => _conceptRepo = conceptRepo;

    [HttpGet]
    public async Task<ActionResult<List<GoalResponse>>> GetAll()
    {
        var goals = await _conceptRepo.GetGoalsAndDesiresAsync(GetUserId());

        return Ok(goals.Select(g =>
        {
            var timeline = g.EntryConceptMaps
                .OrderBy(m => m.Entry.CreatedAt)
                .Select(m => new GoalTimelineEntry(
                    m.Entry.CreatedAt,
                    m.Entry.Content.Length > 100 ? m.Entry.Content[..100] + "..." : m.Entry.Content,
                    g.Type == "desire" ? "desire" : "progress"
                ))
                .ToList();

            var isAchieved = g.Type == "goal" && g.EntryConceptMaps.Count > 1;

            return new GoalResponse(
                g.Id,
                g.Label,
                isAchieved ? "achieved" : "in_progress",
                g.FirstSeenAt,
                isAchieved ? g.LastSeenAt : null,
                timeline
            );
        }).ToList());
    }
}
