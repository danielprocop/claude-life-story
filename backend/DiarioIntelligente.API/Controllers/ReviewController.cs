using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : AuthenticatedController
{
    private readonly IAiService _aiService;
    private readonly IEntryRepository _entryRepo;
    private readonly IConceptRepository _conceptRepo;
    private readonly IEnergyLogRepository _energyRepo;

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
                new List<string>(), new List<string>(), DateTime.UtcNow, new List<ReviewSourceEntry>()));

        var (from, to) = period.ToLower() switch
        {
            "daily" => (DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow),
            "weekly" => (DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow),
            "monthly" => (DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow),
            _ => (DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow)
        };

        var userId = GetUserId();
        var entries = await _entryRepo.GetByDateRangeAsync(userId, from, to);
        if (!entries.Any())
            return Ok(new ReviewResponse($"Nessuna entry trovata per il periodo {period}.", period,
                new List<string>(), new List<string>(), new List<string>(),
                new List<string>(), new List<string>(), DateTime.UtcNow, new List<ReviewSourceEntry>()));

        var entriesContent = string.Join("\n\n---\n\n", entries.Select(e =>
            $"[{e.CreatedAt:dd/MM/yyyy HH:mm}]\n{e.Content}"));

        var concepts = await _conceptRepo.GetByUserAsync(userId);
        var conceptsSummary = string.Join(", ", concepts
            .OrderByDescending(c => c.LastSeenAt)
            .Take(20)
            .Select(c => $"{c.Label} ({c.Type})"));

        var energyLogs = await _energyRepo.GetByDateRangeAsync(userId, from, to);
        var energySummary = energyLogs.Any()
            ? $"Media energia: {energyLogs.Average(e => e.EnergyLevel):F1}, Media stress: {energyLogs.Average(e => e.StressLevel):F1}. " +
              string.Join("; ", energyLogs.Select(e => $"{e.RecordedAt:dd/MM}: E={e.EnergyLevel} S={e.StressLevel}"))
            : "Nessun dato energia/stress disponibile.";

        var review = await _aiService.GenerateReviewAsync(period, entriesContent, conceptsSummary, energySummary);
        var sources = entries
            .OrderByDescending(e => e.CreatedAt)
            .Take(8)
            .Select(e => new ReviewSourceEntry(
                e.Id,
                e.CreatedAt,
                e.Content.Length > 140 ? e.Content[..140] + "..." : e.Content
            ))
            .ToList();

        return Ok(review with { Sources = sources });
    }
}
