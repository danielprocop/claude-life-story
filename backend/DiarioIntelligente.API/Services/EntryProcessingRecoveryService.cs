using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public sealed class EntryProcessingRecoveryService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EntryProcessingRecoveryService> _logger;

    public EntryProcessingRecoveryService(
        IServiceProvider serviceProvider,
        ILogger<EntryProcessingRecoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processingQueue = scope.ServiceProvider.GetRequiredService<EntryProcessingQueue>();
        var rebuildQueue = scope.ServiceProvider.GetRequiredService<UserMemoryRebuildQueue>();

        var usersMissingGraph = await db.Entries
            .Select(entry => entry.UserId)
            .Distinct()
            .Where(userId => !db.CanonicalEntities.Any(entity => entity.UserId == userId))
            .ToListAsync(cancellationToken);

        var usersNeedingRebuild = await db.Entries
            .Where(entry =>
                entry.UpdatedAt != null &&
                !db.EntryProcessingStates.Any(state =>
                    state.EntryId == entry.Id &&
                    state.SourceUpdatedAt == entry.UpdatedAt))
            .Select(entry => entry.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rebuildUsers = usersNeedingRebuild
            .Concat(usersMissingGraph)
            .Distinct()
            .ToList();

        foreach (var userId in rebuildUsers)
            await rebuildQueue.EnqueueAsync(userId, cancellationToken);

        var pendingEntries = await db.Entries
            .Where(entry =>
                !rebuildUsers.Contains(entry.UserId) &&
                entry.UpdatedAt == null &&
                !db.EntryProcessingStates.Any(state => state.EntryId == entry.Id))
            .OrderBy(entry => entry.CreatedAt)
            .Select(entry => new PendingEntry(entry.Id, entry.UserId))
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var entry in pendingEntries)
            await processingQueue.EnqueueAsync(new EntryProcessingJob(entry.Id, entry.UserId), cancellationToken);

        _logger.LogInformation(
            "Recovered {EntryCount} entry jobs and {UserRebuildCount} user rebuild jobs on startup.",
            pendingEntries.Count,
            rebuildUsers.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record PendingEntry(Guid Id, Guid UserId);
}
