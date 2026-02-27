using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiarioIntelligente.AI.Services;

public class NoOpAiService : IAiService
{
    private readonly ILogger<NoOpAiService> _logger;

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

    public float CosineSimilarity(float[] a, float[] b) => 0f;
}
