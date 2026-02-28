using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnergyController : AuthenticatedController
{
    private readonly IEnergyLogRepository _energyRepo;

    public EnergyController(IEnergyLogRepository energyRepo)
    {
        _energyRepo = energyRepo;
    }

    [HttpGet]
    public async Task<ActionResult<EnergyTrendResponse>> GetTrend([FromQuery] int days = 30)
    {
        var logs = await _energyRepo.GetByUserAsync(GetUserId(), days);

        var dataPoints = logs
            .OrderBy(l => l.RecordedAt)
            .Select(l => new EnergyDataPoint(l.RecordedAt, l.EnergyLevel, l.StressLevel, l.DominantEmotion))
            .ToList();

        var avgEnergy = logs.Any() ? logs.Average(l => l.EnergyLevel) : 0;
        var avgStress = logs.Any() ? logs.Average(l => l.StressLevel) : 0;

        var topEmotions = logs
            .Where(l => !string.IsNullOrEmpty(l.DominantEmotion))
            .GroupBy(l => l.DominantEmotion!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        // Simple correlations based on data
        var correlations = new List<EnergyCorrelation>();
        if (logs.Count >= 5)
        {
            var highEnergyEmotions = logs.Where(l => l.EnergyLevel >= 7 && !string.IsNullOrEmpty(l.DominantEmotion))
                .GroupBy(l => l.DominantEmotion!).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (highEnergyEmotions != null)
                correlations.Add(new EnergyCorrelation(highEnergyEmotions.Key, "Energia alta", (float)highEnergyEmotions.Count() / logs.Count));

            var highStressEmotions = logs.Where(l => l.StressLevel >= 7 && !string.IsNullOrEmpty(l.DominantEmotion))
                .GroupBy(l => l.DominantEmotion!).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (highStressEmotions != null)
                correlations.Add(new EnergyCorrelation(highStressEmotions.Key, "Stress alto", (float)highStressEmotions.Count() / logs.Count));
        }

        return Ok(new EnergyTrendResponse(dataPoints, Math.Round(avgEnergy, 1), Math.Round(avgStress, 1), topEmotions, correlations));
    }
}
