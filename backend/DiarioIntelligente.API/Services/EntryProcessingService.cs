using System.Text.Json;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public class EntryProcessingService : BackgroundService
{
    private static readonly Regex TimeTokenRegex =
        new(@"^\d{1,2}(?::\d{2})?$", RegexOptions.Compiled);
    private static readonly HashSet<string> NonPersonStopTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "alle",
        "alla",
        "oggi",
        "ieri",
        "domani",
        "sono",
        "siamo",
        "abbiamo",
        "ho",
        "poi",
        "con",
        "mia",
        "mio",
        "le",
        "la",
        "il",
        "un",
        "una",
        "devo",
        "mi",
        "ha",
        "hanno"
    };
    private static readonly string[] SportsContextHints =
    {
        "giocher",
        "partita",
        "match",
        "campionato",
        "serie a",
        "champions",
        "derby",
        "gol",
        "allenatore",
        "squadra",
        "club",
        "vs",
        "contro"
    };
    private static readonly HashSet<string> KnownTeamTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "milan",
        "inter",
        "juventus",
        "roma",
        "lazio",
        "napoli",
        "atalanta",
        "fiorentina",
        "torino",
        "bologna",
        "genoa",
        "verona",
        "parma",
        "monza",
        "como",
        "udinese",
        "cagliari",
        "lecce",
        "empoli",
        "arsenal",
        "barcelona",
        "realmadrid"
    };

    private readonly EntryProcessingQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EntryProcessingService> _logger;

    public EntryProcessingService(
        EntryProcessingQueue queue,
        IServiceProvider serviceProvider,
        ILogger<EntryProcessingService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Entry processing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Processing entry {EntryId} for user {UserId}", job.EntryId, job.UserId);

                await ProcessEntryAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing entry from queue");
            }
        }

        _logger.LogInformation("Entry processing service stopped");
    }

    private async Task ProcessEntryAsync(EntryProcessingJob job, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entryRepo = scope.ServiceProvider.GetRequiredService<IEntryRepository>();
        var conceptRepo = scope.ServiceProvider.GetRequiredService<IConceptRepository>();
        var connectionRepo = scope.ServiceProvider.GetRequiredService<IConnectionRepository>();
        var insightRepo = scope.ServiceProvider.GetRequiredService<IInsightRepository>();
        var energyRepo = scope.ServiceProvider.GetRequiredService<IEnergyLogRepository>();
        var searchProjectionService = scope.ServiceProvider.GetRequiredService<ISearchProjectionService>();
        var cognitiveGraphService = scope.ServiceProvider.GetRequiredService<ICognitiveGraphService>();
        var entry = await entryRepo.GetByIdAsync(job.EntryId, job.UserId);

        if (entry == null)
        {
            _logger.LogInformation(
                "Entry {EntryId} for user {UserId} no longer exists. Skipping processing.",
                job.EntryId,
                job.UserId);
            await searchProjectionService.DeleteEntryAsync(job.EntryId, job.UserId, ct);
            var staleState = await db.EntryProcessingStates
                .FirstOrDefaultAsync(state => state.EntryId == job.EntryId, ct);
            if (staleState != null)
            {
                db.EntryProcessingStates.Remove(staleState);
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        var content = entry.Content;
        var analysis = new DiarioIntelligente.Core.DTOs.AiAnalysisResult();
        float[] embedding = Array.Empty<float>();
        var hasAiAnalysis = aiService.IsConfigured;

        // Step 1: Generate embedding
        _logger.LogInformation("Generating embedding for entry {EntryId}", job.EntryId);
        embedding = await aiService.GetEmbeddingAsync(content);
        if (embedding.Length > 0)
        {
            var embeddingJson = JsonSerializer.Serialize(embedding);
            await entryRepo.UpdateEmbeddingAsync(job.EntryId, embeddingJson);
        }

        // Step 2: Analyze entry with AI
        if (hasAiAnalysis)
        {
            _logger.LogInformation("Analyzing entry {EntryId} with AI", job.EntryId);
            analysis = await aiService.AnalyzeEntryAsync(content);
            analysis = await SanitizeAnalysisAsync(db, entry.UserId, content, analysis, ct);
        }

        // Step 3: Update canonical graph and ledger
        await cognitiveGraphService.ProcessEntryAsync(entry, analysis, ct);

        if (!hasAiAnalysis)
        {
            _logger.LogInformation("AI not configured — entry {EntryId} saved with heuristic graph processing only", job.EntryId);
            var projectedWithoutAi = await entryRepo.GetByIdAsync(job.EntryId, job.UserId);
            if (projectedWithoutAi != null)
                await searchProjectionService.ProjectEntryAsync(projectedWithoutAi, ct);
            await UpsertProcessingStateAsync(db, entry, hasAiAnalysis, ct);
            return;
        }

        // Step 4: Save energy/stress log
        var dominantEmotion = analysis.Emotions.FirstOrDefault();
        await energyRepo.CreateAsync(new EnergyLog
        {
            Id = Guid.NewGuid(),
            EntryId = job.EntryId,
            UserId = job.UserId,
            EnergyLevel = analysis.EnergyLevel,
            StressLevel = analysis.StressLevel,
            DominantEmotion = dominantEmotion,
            RecordedAt = DateTime.UtcNow
        });
        _logger.LogInformation("Energy log saved: E={Energy} S={Stress} for entry {EntryId}",
            analysis.EnergyLevel, analysis.StressLevel, job.EntryId);

        // Step 5: Save concepts and map them to entry
        var entryConcepts = new List<Concept>();
        foreach (var extracted in analysis.Concepts)
        {
            var concept = await conceptRepo.FindByLabelAndTypeAsync(job.UserId, extracted.Label, extracted.Type);
            if (concept == null)
            {
                concept = new Concept
                {
                    Id = Guid.NewGuid(),
                    UserId = job.UserId,
                    Label = extracted.Label,
                    Type = extracted.Type,
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                };
                concept = await conceptRepo.CreateAsync(concept);
            }
            else
            {
                await conceptRepo.UpdateLastSeenAsync(concept.Id, DateTime.UtcNow);
            }

            entryConcepts.Add(concept);

            await conceptRepo.AddEntryConceptMapAsync(new EntryConceptMap
            {
                EntryId = job.EntryId,
                ConceptId = concept.Id,
                Relevance = 1.0f
            });
        }

        // Also create concepts for goal signals
        foreach (var signal in analysis.GoalSignals)
        {
            var concept = await conceptRepo.FindByLabelAndTypeAsync(job.UserId, signal.Text, signal.Type);
            if (concept == null)
            {
                concept = new Concept
                {
                    Id = Guid.NewGuid(),
                    UserId = job.UserId,
                    Label = signal.Text,
                    Type = signal.Type,
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                };
                await conceptRepo.CreateAsync(concept);
            }
            else
            {
                await conceptRepo.UpdateLastSeenAsync(concept.Id, DateTime.UtcNow);
            }
        }

        // Step 6: Find semantic connections via embedding similarity
        if (embedding.Length > 0)
        {
            _logger.LogInformation("Finding semantic connections for entry {EntryId}", job.EntryId);
            var previousEntries = await entryRepo.GetEntriesWithEmbeddingsAsync(job.UserId);

            foreach (var prev in previousEntries)
            {
                if (prev.Id == job.EntryId || string.IsNullOrEmpty(prev.EmbeddingVector))
                    continue;

                var prevEmbedding = JsonSerializer.Deserialize<float[]>(prev.EmbeddingVector);
                if (prevEmbedding == null) continue;

                var similarity = aiService.CosineSimilarity(embedding, prevEmbedding);

                if (similarity > 0.82f)
                {
                    _logger.LogInformation(
                        "Found semantic connection between entries {EntryA} and {EntryB} (similarity: {Similarity:F3})",
                        job.EntryId, prev.Id, similarity);
                }
            }
        }

        // Step 7: Build connections between concepts that co-occur in this entry
        for (int i = 0; i < entryConcepts.Count; i++)
        {
            for (int j = i + 1; j < entryConcepts.Count; j++)
            {
                var a = entryConcepts[i];
                var b = entryConcepts[j];

                var existing = await connectionRepo.GetAsync(a.Id, b.Id);
                if (existing != null)
                {
                    var newStrength = Math.Min(1.0f, existing.Strength + 0.1f);
                    await connectionRepo.UpdateStrengthAsync(a.Id, b.Id, newStrength);
                }
                else
                {
                    await connectionRepo.CreateAsync(new Connection
                    {
                        ConceptAId = a.Id,
                        ConceptBId = b.Id,
                        Strength = 0.3f,
                        Type = "co_occurrence"
                    });
                }
            }
        }

        // Step 8: Check for goal completions → generate insights
        foreach (var completion in analysis.GoalCompletions)
        {
            var matchingDesire = await conceptRepo.FindByLabelAndTypeAsync(
                job.UserId, completion.MatchesDesire, "desire");

            if (matchingDesire != null)
            {
                _logger.LogInformation(
                    "Goal completion detected: '{Completion}' matches desire '{Desire}'",
                    completion.Text, completion.MatchesDesire);

                await insightRepo.CreateAsync(new Insight
                {
                    Id = Guid.NewGuid(),
                    UserId = job.UserId,
                    Content = $"Obiettivo raggiunto! Avevi scritto di voler \"{completion.MatchesDesire}\" " +
                              $"il {matchingDesire.FirstSeenAt:dd/MM/yyyy}, e ora: \"{completion.Text}\"",
                    GeneratedAt = DateTime.UtcNow,
                    Type = "goal_completed"
                });
            }
        }

        var projectedEntry = await entryRepo.GetByIdAsync(job.EntryId, job.UserId);
        if (projectedEntry != null)
            await searchProjectionService.ProjectEntryAsync(projectedEntry, ct);

        await UpsertProcessingStateAsync(db, entry, hasAiAnalysis, ct);

        _logger.LogInformation("Entry {EntryId} processing completed", job.EntryId);
    }

    private static async Task<AiAnalysisResult> SanitizeAnalysisAsync(
        AppDbContext db,
        Guid userId,
        string content,
        AiAnalysisResult analysis,
        CancellationToken ct)
    {
        if (analysis.Concepts.Count == 0)
            return analysis;

        var kindOverrides = await db.PersonalPolicies
            .Where(policy =>
                policy.UserId == userId &&
                policy.PolicyKey == "entity_kind_override" &&
                policy.Scope != null &&
                policy.Scope.StartsWith("name:"))
            .Select(policy => new { policy.Scope, policy.PolicyValue })
            .ToListAsync(ct);

        var overridesByName = kindOverrides
            .Where(item => !string.IsNullOrWhiteSpace(item.Scope) && item.Scope!.Length > 5)
            .ToDictionary(
                item => item.Scope![5..],
                item => NormalizePolicyKind(item.PolicyValue),
                StringComparer.OrdinalIgnoreCase);

        var nonPersonLabels = analysis.Concepts
            .Where(concept =>
            {
                var type = NormalizeToken(concept.Type);
                return !string.IsNullOrWhiteSpace(concept.Label) && type != "person";
            })
            .Select(concept => NormalizeToken(concept.Label))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sanitized = new List<ExtractedConcept>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var concept in analysis.Concepts)
        {
            if (string.IsNullOrWhiteSpace(concept.Label))
                continue;

            var rawLabel = concept.Label.Trim();
            var normalizedLabel = NormalizeToken(rawLabel);
            if (string.IsNullOrWhiteSpace(normalizedLabel))
                continue;

            var normalizedType = NormalizeToken(concept.Type);
            var targetType = ResolveTargetConceptType(
                rawLabel,
                normalizedLabel,
                normalizedType,
                content,
                nonPersonLabels,
                overridesByName);

            if (targetType == null)
                continue;

            var dedupeKey = $"{normalizedLabel}:{targetType}";
            if (!seen.Add(dedupeKey))
                continue;

            sanitized.Add(new ExtractedConcept
            {
                Label = rawLabel,
                Type = targetType
            });
        }

        analysis.Concepts = sanitized;
        return analysis;
    }

    private static string? ResolveTargetConceptType(
        string rawLabel,
        string normalizedLabel,
        string normalizedType,
        string content,
        IReadOnlySet<string> nonPersonLabels,
        IReadOnlyDictionary<string, string> overridesByName)
    {
        if (overridesByName.TryGetValue(normalizedLabel, out var overrideKind))
        {
            if (overrideKind == "not_person")
                return normalizedType == "person" ? null : string.IsNullOrWhiteSpace(normalizedType) ? "generic" : normalizedType;

            return overrideKind;
        }

        if (normalizedType != "person")
            return string.IsNullOrWhiteSpace(normalizedType) ? "generic" : normalizedType;

        if (NonPersonStopTokens.Contains(normalizedLabel))
            return null;

        if (TimeTokenRegex.IsMatch(rawLabel))
            return null;

        if (nonPersonLabels.Contains(normalizedLabel))
            return null;

        if (IsLikelyTeamMention(content, rawLabel, normalizedLabel))
            return "team";

        return "person";
    }

    private static bool IsLikelyTeamMention(string content, string rawLabel, string normalizedLabel)
    {
        var lower = content.ToLowerInvariant();
        var hasSportsContext = SportsContextHints.Any(lower.Contains);
        if (!hasSportsContext)
            return false;

        if (KnownTeamTokens.Contains(normalizedLabel))
            return true;

        var escaped = Regex.Escape(rawLabel.Trim());
        return Regex.IsMatch(content, $@"\b(?:il|lo|la|i|gli|le|l')\s*{escaped}\b", RegexOptions.IgnoreCase);
    }

    private static string NormalizePolicyKind(string rawValue)
    {
        var normalized = NormalizeToken(rawValue);
        return normalized switch
        {
            "persona" => "person",
            "luogo" => "place",
            "squadra" => "team",
            "organizzazione" => "organization",
            "progetto" => "project",
            "attivita" => "activity",
            "emozione" => "emotion",
            "idea" => "idea",
            "problema" => "problem",
            "finanza" => "finance",
            "notperson" or "nonperson" => "not_person",
            _ => normalized
        };
    }

    private static string NormalizeToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static async Task UpsertProcessingStateAsync(
        AppDbContext db,
        Entry entry,
        bool usedAiAnalysis,
        CancellationToken ct)
    {
        var state = await db.EntryProcessingStates.FirstOrDefaultAsync(x => x.EntryId == entry.Id, ct);
        if (state == null)
        {
            db.EntryProcessingStates.Add(new EntryProcessingState
            {
                EntryId = entry.Id,
                UserId = entry.UserId,
                SourceUpdatedAt = entry.UpdatedAt,
                LastProcessedAt = DateTime.UtcNow,
                UsedAiAnalysis = usedAiAnalysis
            });
        }
        else
        {
            state.UserId = entry.UserId;
            state.SourceUpdatedAt = entry.UpdatedAt;
            state.LastProcessedAt = DateTime.UtcNow;
            state.UsedAiAnalysis = usedAiAnalysis;
        }

        await db.SaveChangesAsync(ct);
    }
}
