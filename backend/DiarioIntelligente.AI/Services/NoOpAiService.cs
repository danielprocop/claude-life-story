using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiarioIntelligente.AI.Services;

public class NoOpAiService : IAiService
{
    private readonly ILogger<NoOpAiService> _logger;

    public bool IsConfigured => false;

    public NoOpAiService(ILogger<NoOpAiService> logger)
    {
        _logger = logger;
    }

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        _logger.LogWarning("OpenAI API key not configured — skipping embedding generation");
        return Task.FromResult(Array.Empty<float>());
    }

    public Task<AiAnalysisResult> AnalyzeEntryAsync(string content)
    {
        _logger.LogWarning("OpenAI API key not configured — skipping entry analysis");
        return Task.FromResult(new AiAnalysisResult());
    }

    public Task<string> ChatWithContextAsync(string userMessage, string entriesContext, string chatHistory)
    {
        _logger.LogWarning("OpenAI API key not configured — chat not available");
        return Task.FromResult("L'AI non è configurata. Aggiungi una chiave API OpenAI per usare la chat.");
    }

    public Task<ReviewResponse> GenerateReviewAsync(string period, string entriesContent, string conceptsSummary, string energySummary)
    {
        _logger.LogWarning("OpenAI API key not configured — review generation not available");
        return Task.FromResult(new ReviewResponse(
            "AI non configurata", period,
            new List<string>(), new List<string>(), new List<string>(),
            new List<string>(), new List<string>(), DateTime.UtcNow));
    }

    public Task<List<string>> DetectPatternsAsync(string entriesContent, string energyData, string conceptsData)
    {
        _logger.LogWarning("OpenAI API key not configured — pattern detection not available");
        return Task.FromResult(new List<string>());
    }

    public float CosineSimilarity(float[] a, float[] b) => 0f;
}
