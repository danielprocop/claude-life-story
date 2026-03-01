using System.Text.Json;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public sealed class FeedbackReplayService : BackgroundService
{
    private readonly FeedbackReplayQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FeedbackReplayService> _logger;

    public FeedbackReplayService(
        FeedbackReplayQueue queue,
        IServiceProvider serviceProvider,
        ILogger<FeedbackReplayService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feedback replay service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback replay worker failed");
            }
        }

        _logger.LogInformation("Feedback replay service stopped");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entryQueue = scope.ServiceProvider.GetRequiredService<EntryProcessingQueue>();
        var rebuildQueue = scope.ServiceProvider.GetRequiredService<UserMemoryRebuildQueue>();
        var searchProjection = scope.ServiceProvider.GetRequiredService<DiarioIntelligente.Core.Interfaces.ISearchProjectionService>();

        var job = await db.FeedbackReplayJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job == null)
            return;

        if (job.Status is "completed" or "failed")
            return;

        job.Status = "running";
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var payload = JsonDocument.Parse(job.PayloadJson).RootElement;
            var entryIds = ReadGuidArray(payload, "entryIds");
            var entityIds = ReadGuidArray(payload, "entityIds");

            if (job.DryRun)
            {
                job.Status = "completed";
                job.CompletedAt = DateTime.UtcNow;
                job.SummaryJson = JsonSerializer.Serialize(new
                {
                    mode = "dry_run",
                    entries = entryIds.Count,
                    entities = entityIds.Count
                });
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            if (job.TargetUserId == null)
            {
                job.Status = "failed";
                job.CompletedAt = DateTime.UtcNow;
                job.Error = "Missing target_user_id.";
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var userId = job.TargetUserId.Value;

            if (entryIds.Count > 0)
            {
                foreach (var entryId in entryIds)
                    await entryQueue.EnqueueAsync(new EntryProcessingJob(entryId, userId), cancellationToken);
            }
            else
            {
                await rebuildQueue.EnqueueAsync(userId, cancellationToken);
            }

            foreach (var entityId in entityIds)
            {
                var entity = await db.CanonicalEntities
                    .Where(x => x.UserId == userId && x.Id == entityId)
                    .Include(x => x.Aliases)
                    .Include(x => x.Evidence)
                    .FirstOrDefaultAsync(cancellationToken);

                if (entity != null)
                    await searchProjection.ProjectEntityAsync(entity, cancellationToken);
            }

            job.Status = "completed";
            job.CompletedAt = DateTime.UtcNow;
            job.SummaryJson = JsonSerializer.Serialize(new
            {
                mode = "apply",
                queuedEntries = entryIds.Count,
                projectedEntities = entityIds.Count
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            job.Status = "failed";
            job.CompletedAt = DateTime.UtcNow;
            job.Error = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Feedback replay job {JobId} failed", jobId);
        }
    }

    private static List<Guid> ReadGuidArray(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
            return new List<Guid>();

        return element
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && Guid.TryParse(x.GetString(), out _))
            .Select(x => Guid.Parse(x.GetString()!))
            .Distinct()
            .ToList();
    }
}

