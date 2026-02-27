using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface IAiService
{
    Task<float[]> GetEmbeddingAsync(string text);
    Task<AiAnalysisResult> AnalyzeEntryAsync(string content);
    float CosineSimilarity(float[] a, float[] b);
    Task<string> ChatWithContextAsync(string userMessage, string entriesContext, string chatHistory);
    Task<ReviewResponse> GenerateReviewAsync(string period, string entriesContent, string conceptsSummary, string energySummary);
    Task<List<string>> DetectPatternsAsync(string entriesContent, string energyData, string conceptsData);
    bool IsConfigured { get; }
}
