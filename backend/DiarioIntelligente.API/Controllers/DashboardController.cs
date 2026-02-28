using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : AuthenticatedController
{
    private readonly IEntryRepository _entryRepo;
    private readonly IConceptRepository _conceptRepo;
    private readonly IGoalItemRepository _goalRepo;
    private readonly IInsightRepository _insightRepo;
    private readonly IEnergyLogRepository _energyRepo;

    public DashboardController(
        IEntryRepository entryRepo,
        IConceptRepository conceptRepo,
        IGoalItemRepository goalRepo,
        IInsightRepository insightRepo,
        IEnergyLogRepository energyRepo)
    {
        _entryRepo = entryRepo;
        _conceptRepo = conceptRepo;
        _goalRepo = goalRepo;
        _insightRepo = insightRepo;
        _energyRepo = energyRepo;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get()
    {
        var userId = GetUserId();
        var totalEntries = await _entryRepo.CountByUserAsync(userId);
        var concepts = await _conceptRepo.GetByUserAsync(userId);
        var goals = await _goalRepo.GetRootGoalsAsync(userId);
        var insights = await _insightRepo.GetByUserAsync(userId);
        var energyLogs = await _energyRepo.GetByUserAsync(userId, 14);
        var (recentEntries, _) = await _entryRepo.GetByUserAsync(userId, 1, 5);

        var activeGoals = goals.Where(g => g.Status == "active").ToList();

        var avgEnergy = energyLogs.Any() ? energyLogs.Average(e => e.EnergyLevel) : 0;
        var avgStress = energyLogs.Any() ? energyLogs.Average(e => e.StressLevel) : 0;

        var stats = new DashboardStats(
            totalEntries,
            concepts.Count,
            activeGoals.Count,
            insights.Count,
            Math.Round(avgEnergy, 1),
            Math.Round(avgStress, 1)
        );

        var energyTrend = energyLogs
            .OrderBy(e => e.RecordedAt)
            .Select(e => new EnergyDataPoint(e.RecordedAt, e.EnergyLevel, e.StressLevel, e.DominantEmotion))
            .ToList();

        var topConcepts = concepts
            .OrderByDescending(c => c.EntryConceptMaps?.Count ?? 0)
            .Take(10)
            .Select(c => new ConceptResponse(c.Id, c.Label, c.Type, c.FirstSeenAt, c.LastSeenAt, c.EntryConceptMaps?.Count ?? 0))
            .ToList();

        var goalResponses = activeGoals.Take(5).Select(MapGoalItem).ToList();

        var recentInsights = insights
            .OrderByDescending(i => i.GeneratedAt)
            .Take(5)
            .Select(i => new InsightResponse(i.Id, i.Content, i.GeneratedAt, i.Type))
            .ToList();

        var entryResponses = recentEntries.Select(e => new EntryListResponse(
            e.Id,
            e.Content.Length > 150 ? e.Content[..150] + "..." : e.Content,
            e.CreatedAt,
            e.EntryConceptMaps?.Count ?? 0
        )).ToList();

        return Ok(new DashboardResponse(stats, energyTrend, topConcepts, goalResponses, recentInsights, entryResponses));
    }

    private static GoalItemResponse MapGoalItem(Core.Models.GoalItem g) => new(
        g.Id, g.Title, g.Description, g.Status, g.CreatedAt, g.CompletedAt, g.ParentGoalId,
        g.SubGoals?.Select(MapGoalItem).ToList() ?? new List<GoalItemResponse>()
    );
}
