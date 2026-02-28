using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface ISearchProjectionService
{
    Task ProjectEntryAsync(Entry entry, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(Guid entryId, Guid userId, CancellationToken cancellationToken = default);
    Task ProjectGoalItemAsync(GoalItem goalItem, CancellationToken cancellationToken = default);
    Task DeleteGoalItemAsync(Guid goalItemId, Guid userId, CancellationToken cancellationToken = default);
    Task ResetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
