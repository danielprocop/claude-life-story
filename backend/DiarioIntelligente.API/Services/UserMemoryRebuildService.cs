using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public sealed class UserMemoryRebuildService : BackgroundService
{
    private readonly UserMemoryRebuildQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserMemoryRebuildService> _logger;

    public UserMemoryRebuildService(
        UserMemoryRebuildQueue queue,
        IServiceProvider serviceProvider,
        ILogger<UserMemoryRebuildService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("User memory rebuild service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var userId = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Rebuilding derived memory for user {UserId}", userId);
                await RebuildUserMemoryAsync(userId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding derived memory");
            }
        }

        _logger.LogInformation("User memory rebuild service stopped");
    }

    private async Task RebuildUserMemoryAsync(Guid userId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entryRepo = scope.ServiceProvider.GetRequiredService<IEntryRepository>();
        var entryQueue = scope.ServiceProvider.GetRequiredService<EntryProcessingQueue>();
        var searchProjectionService = scope.ServiceProvider.GetRequiredService<ISearchProjectionService>();
        var cognitiveGraphService = scope.ServiceProvider.GetRequiredService<ICognitiveGraphService>();

        var entryIds = await db.Entries
            .Where(e => e.UserId == userId)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var conceptIds = await db.Concepts
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (entryIds.Count > 0)
        {
            await db.EntryConceptMaps
                .Where(map => entryIds.Contains(map.EntryId))
                .ExecuteDeleteAsync(ct);

            await db.EnergyLogs
                .Where(log => log.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await db.Entries
                .Where(entry => entry.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(entry => entry.EmbeddingVector, (string?)null),
                    ct);

            await db.EntryProcessingStates
                .Where(state => state.UserId == userId)
                .ExecuteDeleteAsync(ct);
        }

        await db.Insights
            .Where(insight => insight.UserId == userId)
            .ExecuteDeleteAsync(ct);

        if (conceptIds.Count > 0)
        {
            await db.Connections
                .Where(connection => conceptIds.Contains(connection.ConceptAId) || conceptIds.Contains(connection.ConceptBId))
                .ExecuteDeleteAsync(ct);

            await db.Concepts
                .Where(concept => concept.UserId == userId)
                .ExecuteDeleteAsync(ct);
        }

        await cognitiveGraphService.ClearUserGraphAsync(userId, ct);
        await searchProjectionService.ResetUserAsync(userId, ct);

        var entries = await entryRepo.GetAllByUserAsync(userId);
        foreach (var entry in entries)
            await entryQueue.EnqueueAsync(new EntryProcessingJob(entry.Id, userId), ct);

        _logger.LogInformation(
            "Queued {EntryCount} entries for derived memory rebuild for user {UserId}",
            entries.Count,
            userId);
    }
}
