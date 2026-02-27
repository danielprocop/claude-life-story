using System.Text.Json;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.API.Services;

public class EntryProcessingService : BackgroundService
{
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
        var entryRepo = scope.ServiceProvider.GetRequiredService<IEntryRepository>();
        var conceptRepo = scope.ServiceProvider.GetRequiredService<IConceptRepository>();
        var connectionRepo = scope.ServiceProvider.GetRequiredService<IConnectionRepository>();
        var insightRepo = scope.ServiceProvider.GetRequiredService<IInsightRepository>();
        var energyRepo = scope.ServiceProvider.GetRequiredService<IEnergyLogRepository>();

        // Step 1: Generate embedding
        _logger.LogInformation("Generating embedding for entry {EntryId}", job.EntryId);
        var embedding = await aiService.GetEmbeddingAsync(job.Content);

        if (embedding.Length == 0)
        {
            _logger.LogInformation("AI not configured — entry {EntryId} saved without analysis", job.EntryId);
            return;
        }

        var embeddingJson = JsonSerializer.Serialize(embedding);
        await entryRepo.UpdateEmbeddingAsync(job.EntryId, embeddingJson);

        // Step 2: Analyze entry with AI
        _logger.LogInformation("Analyzing entry {EntryId} with AI", job.EntryId);
        var analysis = await aiService.AnalyzeEntryAsync(job.Content);

        // Step 3: Save energy/stress log
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

        // Step 4: Save concepts and map them to entry
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

        // Step 5: Find semantic connections via embedding similarity
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

        // Step 6: Build connections between concepts that co-occur in this entry
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

        // Step 7: Check for goal completions → generate insights
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

        _logger.LogInformation("Entry {EntryId} processing completed", job.EntryId);
    }
}
