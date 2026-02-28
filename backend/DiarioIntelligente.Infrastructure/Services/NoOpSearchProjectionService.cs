using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using Microsoft.Extensions.Logging;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class NoOpSearchProjectionService : ISearchProjectionService
{
    private readonly ILogger<NoOpSearchProjectionService> _logger;

    public NoOpSearchProjectionService(ILogger<NoOpSearchProjectionService> logger)
    {
        _logger = logger;
    }

    public Task ProjectEntryAsync(Entry entry, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection skipped for entry {EntryId} and user {UserId}. No search backend configured.",
            entry.Id,
            entry.UserId);
        return Task.CompletedTask;
    }

    public Task DeleteEntryAsync(Guid entryId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection delete skipped for entry {EntryId} and user {UserId}. No search backend configured.",
            entryId,
            userId);
        return Task.CompletedTask;
    }

    public Task ProjectEntityAsync(CanonicalEntity entity, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection skipped for entity {EntityId} and user {UserId}. No search backend configured.",
            entity.Id,
            entity.UserId);
        return Task.CompletedTask;
    }

    public Task DeleteEntityAsync(Guid entityId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection delete skipped for entity {EntityId} and user {UserId}. No search backend configured.",
            entityId,
            userId);
        return Task.CompletedTask;
    }

    public Task ProjectGoalItemAsync(GoalItem goalItem, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection skipped for goal item {GoalItemId} and user {UserId}. No search backend configured.",
            goalItem.Id,
            goalItem.UserId);
        return Task.CompletedTask;
    }

    public Task DeleteGoalItemAsync(Guid goalItemId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection delete skipped for goal item {GoalItemId} and user {UserId}. No search backend configured.",
            goalItemId,
            userId);
        return Task.CompletedTask;
    }

    public Task ResetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Search projection reset skipped for user {UserId}. No search backend configured.",
            userId);
        return Task.CompletedTask;
    }
}
