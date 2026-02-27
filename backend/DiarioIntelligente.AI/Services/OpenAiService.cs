using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using DiarioIntelligente.AI.Configuration;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiarioIntelligente.AI.Services;

public class OpenAiService : IAiService
{
    private readonly OpenAIClient _client;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(IOptions<OpenAiSettings> settings, ILogger<OpenAiService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new OpenAIClient(_settings.ApiKey);
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await RetryAsync(async () =>
            {
                return await _client.GetEmbeddingsAsync(
                    new EmbeddingsOptions(_settings.EmbeddingModel, new[] { text })
                );
            });

            var vector = response.Value.Data[0].Embedding.ToArray();
            sw.Stop();

            _logger.LogInformation(
                "Embedding generated in {ElapsedMs}ms, dimensions: {Dimensions}, model: {Model}",
                sw.ElapsedMilliseconds, vector.Length, _settings.EmbeddingModel);

            return vector;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Embedding generation failed after {ElapsedMs}ms, model: {Model}",
                sw.ElapsedMilliseconds, _settings.EmbeddingModel);
            throw;
        }
    }

    public async Task<AiAnalysisResult> AnalyzeEntryAsync(string content)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await RetryAsync(async () =>
            {
                return await _client.GetChatCompletionsAsync(
                    new ChatCompletionsOptions
                    {
                        DeploymentName = _settings.Model,
                        Messages =
                        {
                            new ChatRequestSystemMessage(@"
Sei un sistema che analizza entry di diari personali.
Estrai le informazioni e restituisci SOLO un JSON valido, senza testo aggiuntivo.
Il JSON deve avere esattamente questa struttura:
{
  ""emotions"": [""string""],
  ""concepts"": [{ ""label"": ""string"", ""type"": ""person|place|desire|goal|activity|emotion"" }],
  ""goalSignals"": [{ ""text"": ""string"", ""type"": ""desire|goal|progress"" }],
  ""goalCompletions"": [{ ""text"": ""string"", ""matchesDesire"": ""string"" }]
}
Se un campo non ha valori, usa un array vuoto []."),
                            new ChatRequestUserMessage($"Analizza questa entry del diario:\n\n{content}")
                        },
                        Temperature = 0.2f
                    }
                );
            });

            var json = response.Value.Choices[0].Message.Content;
            sw.Stop();

            var usage = response.Value.Usage;
            _logger.LogInformation(
                "Entry analyzed in {ElapsedMs}ms, tokens: {Prompt}/{Completion}/{Total}, model: {Model}",
                sw.ElapsedMilliseconds,
                usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens,
                _settings.Model);

            var result = JsonSerializer.Deserialize<AiAnalysisResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new AiAnalysisResult();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Entry analysis failed after {ElapsedMs}ms, model: {Model}",
                sw.ElapsedMilliseconds, _settings.Model);
            throw;
        }
    }

    public float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        var dot = 0f;
        var magA = 0f;
        var magB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude == 0 ? 0 : (float)(dot / magnitude);
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (RequestFailedException ex) when (attempt < maxRetries && (ex.Status == 429 || ex.Status >= 500))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(
                    "OpenAI API call failed (attempt {Attempt}/{MaxRetries}, status {Status}). Retrying in {Delay}s...",
                    attempt, maxRetries, ex.Status, delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        // This should never be reached, but just in case
        return await action();
    }
}
