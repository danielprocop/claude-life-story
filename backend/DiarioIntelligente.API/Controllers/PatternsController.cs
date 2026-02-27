using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatternsController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IEntryRepository _entryRepo;
    private readonly IConceptRepository _conceptRepo;
    private readonly IEnergyLogRepository _energyRepo;
    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public PatternsController(
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

    [HttpGet]
    public async Task<ActionResult<List<string>>> DetectPatterns([FromQuery] int days = 30)
    {
        if (!_aiService.IsConfigured)
            return Ok(new List<string>());

        var from = DateTime.UtcNow.Date.AddDays(-days);
        var entries = await _entryRepo.GetByDateRangeAsync(DemoUserId, from, DateTime.UtcNow);
        if (entries.Count < 3)
            return Ok(new List<string> { "Servono almeno 3 entry per rilevare pattern." });

        var entriesContent = string.Join("\n\n---\n\n", entries.Select(e =>
            $"[{e.CreatedAt:dd/MM/yyyy HH:mm}]\n{e.Content}"));

        var concepts = await _conceptRepo.GetByUserAsync(DemoUserId);
        var conceptsData = string.Join(", ", concepts
            .OrderByDescending(c => c.EntryConceptMaps?.Count ?? 0)
            .Take(30)
            .Select(c => $"{c.Label} ({c.Type}, visto {c.EntryConceptMaps?.Count ?? 0} volte)"));

        var energyLogs = await _energyRepo.GetByDateRangeAsync(DemoUserId, from, DateTime.UtcNow);
        var energyData = energyLogs.Any()
            ? string.Join("; ", energyLogs.OrderBy(e => e.RecordedAt)
                .Select(e => $"{e.RecordedAt:dd/MM}: E={e.EnergyLevel} S={e.StressLevel} {e.DominantEmotion ?? ""}"))
            : "Nessun dato energia/stress.";

        var patterns = await _aiService.DetectPatternsAsync(entriesContent, energyData, conceptsData);
        return Ok(patterns);
    }
}
