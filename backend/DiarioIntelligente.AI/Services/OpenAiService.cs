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

    public bool IsConfigured => true;

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
  ""concepts"": [{ ""label"": ""string"", ""type"": ""person|place|team|organization|project|activity|emotion|idea|problem|finance|object|vehicle|brand|product_model|year|date|time|amount|desire|goal|progress|not_entity"" }],
  ""goalSignals"": [{ ""text"": ""string"", ""type"": ""desire|goal|progress"" }],
  ""goalCompletions"": [{ ""text"": ""string"", ""matchesDesire"": ""string"" }],
  ""energyLevel"": 5,
  ""stressLevel"": 5
}
- emotions: emozioni rilevate nel testo
- concepts: entità estratte (persone, luoghi, progetti, decisioni, problemi, abitudini, attività, obiettivi, desideri, emozioni)
- goalSignals: segnali di obiettivi o desideri espressi
- goalCompletions: obiettivi che sembrano completati rispetto a desideri precedenti
- energyLevel: livello di energia percepito (1=molto basso, 10=molto alto), dedotto dal tono e contenuto
- stressLevel: livello di stress percepito (1=molto basso, 10=molto alto), dedotto dal tono e contenuto
- type=person solo per esseri umani reali (nome persona, familiare, collega, amico).
- NON classificare mai come person articoli, preposizioni o avverbi (es: il, la, un, con, alle, oggi).
- Se il termine e una squadra o club sportivo (es: Milan, Inter, Juventus) usa type=team.
- Se il termine e un'auto o veicolo usa type=vehicle.
- Se il termine e una marca auto usa type=brand.
- Se il termine e un modello auto (es: Citroen DS 5, Audi A3, BMW X1) usa type=product_model.
- Se il termine e un anno (es: 2012) usa type=year.
- Se il termine e un orario (es: 12:30) usa type=time.
- Se il termine e un importo con valuta usa type=amount.
- Se compare lo stesso label con type non-person, evita duplicato come person.
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

    public async Task<string> ChatWithContextAsync(string userMessage, string entriesContext, string chatHistory)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await RetryAsync(async () =>
            {
                return await _client.GetChatCompletionsAsync(
                    new ChatCompletionsOptions
                    {
                        DeploymentName = _settings.InsightModel,
                        Messages =
                        {
                            new ChatRequestSystemMessage($@"
Sei un assistente personale intelligente che ha accesso al diario dell'utente.
Il tuo compito è rispondere alle domande dell'utente basandoti ESCLUSIVAMENTE sulle sue entry del diario.
Se non trovi informazioni rilevanti, dillo chiaramente.
Cita sempre le date e i passaggi rilevanti quando rispondi.
Rispondi in italiano, in modo naturale e conciso.

=== ENTRY DEL DIARIO RILEVANTI ===
{entriesContext}

=== CRONOLOGIA CHAT RECENTE ===
{chatHistory}"),
                            new ChatRequestUserMessage(userMessage)
                        },
                        Temperature = 0.3f,
                        MaxTokens = 1000
                    }
                );
            });

            sw.Stop();
            var usage = response.Value.Usage;
            _logger.LogInformation(
                "Chat response generated in {ElapsedMs}ms, tokens: {Total}, model: {Model}",
                sw.ElapsedMilliseconds, usage.TotalTokens, _settings.InsightModel);

            return response.Value.Choices[0].Message.Content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Chat response generation failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<ReviewResponse> GenerateReviewAsync(string period, string entriesContent, string conceptsSummary, string energySummary)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await RetryAsync(async () =>
            {
                return await _client.GetChatCompletionsAsync(
                    new ChatCompletionsOptions
                    {
                        DeploymentName = _settings.InsightModel,
                        Messages =
                        {
                            new ChatRequestSystemMessage($@"
Sei un coach personale che genera review periodiche basate sul diario dell'utente.
Analizza le entry del periodo indicato e genera una review strutturata.
Restituisci SOLO un JSON valido con questa struttura:
{{
  ""summary"": ""riassunto generale del periodo in 2-3 frasi"",
  ""keyThemes"": [""tema1"", ""tema2""],
  ""accomplishments"": [""risultato1"", ""risultato2""],
  ""challenges"": [""sfida1"", ""sfida2""],
  ""patterns"": [""pattern1"", ""pattern2""],
  ""suggestions"": [""suggerimento1"", ""suggerimento2""]
}}
- summary: breve riassunto del periodo
- keyThemes: temi principali emersi
- accomplishments: cose portate a termine o progressi concreti
- challenges: difficoltà e blocchi
- patterns: pattern ricorrenti (positivi e negativi)
- suggestions: suggerimenti concreti e azionabili (non motivazionali generici)

=== DATI ENERGIA/STRESS ===
{energySummary}

=== CONCETTI ESTRATTI ===
{conceptsSummary}"),
                            new ChatRequestUserMessage($"Genera una review {period} basata su queste entry:\n\n{entriesContent}")
                        },
                        Temperature = 0.3f,
                        MaxTokens = 1500
                    }
                );
            });

            var json = response.Value.Choices[0].Message.Content;
            sw.Stop();

            _logger.LogInformation("Review generated in {ElapsedMs}ms, model: {Model}",
                sw.ElapsedMilliseconds, _settings.InsightModel);

            var parsed = JsonSerializer.Deserialize<JsonElement>(json);

            return new ReviewResponse(
                parsed.GetProperty("summary").GetString() ?? "",
                period,
                parsed.GetProperty("keyThemes").EnumerateArray().Select(x => x.GetString() ?? "").ToList(),
                parsed.GetProperty("accomplishments").EnumerateArray().Select(x => x.GetString() ?? "").ToList(),
                parsed.GetProperty("challenges").EnumerateArray().Select(x => x.GetString() ?? "").ToList(),
                parsed.GetProperty("patterns").EnumerateArray().Select(x => x.GetString() ?? "").ToList(),
                parsed.GetProperty("suggestions").EnumerateArray().Select(x => x.GetString() ?? "").ToList(),
                DateTime.UtcNow,
                new List<ReviewSourceEntry>()
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Review generation failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<List<string>> DetectPatternsAsync(string entriesContent, string energyData, string conceptsData)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await RetryAsync(async () =>
            {
                return await _client.GetChatCompletionsAsync(
                    new ChatCompletionsOptions
                    {
                        DeploymentName = _settings.InsightModel,
                        Messages =
                        {
                            new ChatRequestSystemMessage($@"
Sei un analista di pattern personali. Analizza le entry del diario, i dati di energia/stress e i concetti estratti.
Identifica pattern ricorrenti come:
- Correlazioni energia/stress con attività specifiche
- Cicli ripetitivi (problemi che tornano, abitudini positive/negative)
- Segnali precoci (es. stress crescente prima di un blocco)
- Progressi su obiettivi vs ""rumore"" (fare tanto senza avanzare)
- Correlazioni tra persone/progetti e stati d'animo

Restituisci SOLO un JSON array di stringhe, ogni stringa è un pattern trovato:
[""pattern1"", ""pattern2"", ...]
Ogni pattern deve essere specifico, basato sui dati, e includere evidenze concrete.
Se non ci sono abbastanza dati, restituisci un array vuoto [].

=== DATI ENERGIA/STRESS ===
{energyData}

=== CONCETTI ESTRATTI ===
{conceptsData}"),
                            new ChatRequestUserMessage($"Analizza questi dati e trova pattern:\n\n{entriesContent}")
                        },
                        Temperature = 0.3f,
                        MaxTokens = 1000
                    }
                );
            });

            var json = response.Value.Choices[0].Message.Content;
            sw.Stop();

            _logger.LogInformation("Pattern detection completed in {ElapsedMs}ms, model: {Model}",
                sw.ElapsedMilliseconds, _settings.InsightModel);

            var patterns = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return patterns;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Pattern detection failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
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

        return await action();
    }
}
