using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public sealed class EntryProcessingRecoveryService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EntryProcessingRecoveryService> _logger;

    public EntryProcessingRecoveryService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EntryProcessingRecoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var hasAiAnalysis = !string.IsNullOrWhiteSpace(_configuration["OpenAI:ApiKey"]);
        if (!hasAiAnalysis)
        {
            _logger.LogInformation(
                "Entry processing recovery skipped because AI analysis is not configured.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processingQueue = scope.ServiceProvider.GetRequiredService<EntryProcessingQueue>();
        var rebuildQueue = scope.ServiceProvider.GetRequiredService<UserMemoryRebuildQueue>();

        var usersNeedingRebuild = await db.Entries
            .Where(entry => entry.UpdatedAt != null &&
                (entry.EmbeddingVector == null ||
                 !db.EnergyLogs.Any(log => log.EntryId == entry.Id && log.RecordedAt >= entry.UpdatedAt)))
            .Select(entry => entry.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in usersNeedingRebuild)
            await rebuildQueue.EnqueueAsync(userId, cancellationToken);

        var pendingEntries = await db.Entries
            .Where(entry => !usersNeedingRebuild.Contains(entry.UserId) &&
                entry.UpdatedAt == null &&
                entry.EmbeddingVector == null &&
                !db.EnergyLogs.Any(log => log.EntryId == entry.Id))
            .OrderBy(entry => entry.CreatedAt)
            .Select(entry => new { entry.Id, entry.UserId })
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var entry in pendingEntries)
            await processingQueue.EnqueueAsync(new EntryProcessingJob(entry.Id, entry.UserId), cancellationToken);

        _logger.LogInformation(
            "Recovered {EntryCount} entry jobs and {UserRebuildCount} user rebuild jobs on startup.",
            pendingEntries.Count,
            usersNeedingRebuild.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
