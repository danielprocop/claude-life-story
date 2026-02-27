using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IEntryRepository _entryRepo;
    private readonly IConceptRepository _conceptRepo;
    private readonly IEnergyLogRepository _energyRepo;
    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ReviewController(
        IAiService aiService,
        IEntryRepository entryRepo,
        IConceptRepository conceptRepo,
        IEnergyLogRepository energyRepo)
    {
        _aiService = aiService;
        _entryRepo = entryRepo;
        _conceptRepo = conceptRepo;
        _energyRepo = energyRepo;
    }

    [HttpGet("{period}")]
    public async Task<ActionResult<ReviewResponse>> Generate(string period)
    {
        if (!_aiService.IsConfigured)
            return Ok(new ReviewResponse("AI non configurata", period,
                new List<string>(), new List<string>(), new List<string>(),
                new List<string>(), new List<string>(), DateTime.UtcNow));

        var (from, to) = period.ToLower() switch
        {
            "daily" => (DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow),
            "weekly" => (DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow),
            "monthly" => (DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow),
            _ => (DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow)
        };

        var entries = await _entryRepo.GetByDateRangeAsync(DemoUserId, from, to);
        if (!entries.Any())
            return Ok(new ReviewResponse($"Nessuna entry trovata per il periodo {period}.", period,
                new List<string>(), new List<string>(), new List<string>(),
                new List<string>(), new List<string>(), DateTime.UtcNow));

        var entriesContent = string.Join("\n\n---\n\n", entries.Select(e =>
            $"[{e.CreatedAt:dd/MM/yyyy HH:mm}]\n{e.Content}"));

        var concepts = await _conceptRepo.GetByUserAsync(DemoUserId);
        var conceptsSummary = string.Join(", ", concepts
            .OrderByDescending(c => c.LastSeenAt)
            .Take(20)
            .Select(c => $"{c.Label} ({c.Type})"));

        var energyLogs = await _energyRepo.GetByDateRangeAsync(DemoUserId, from, to);
        var energySummary = energyLogs.Any()
            ? $"Media energia: {energyLogs.Average(e => e.EnergyLevel):F1}, Media stress: {energyLogs.Average(e => e.StressLevel):F1}. " +
              string.Join("; ", energyLogs.Select(e => $"{e.RecordedAt:dd/MM}: E={e.EnergyLevel} S={e.StressLevel}"))
            : "Nessun dato energia/stress disponibile.";

        var review = await _aiService.GenerateReviewAsync(period, entriesContent, conceptsSummary, energySummary);
        return Ok(review);
    }
}
