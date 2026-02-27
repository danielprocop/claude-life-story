using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IGoalItemRepository
{
    Task<GoalItem> CreateAsync(GoalItem goal);
    Task<GoalItem?> GetByIdAsync(Guid id, Guid userId);
    Task<List<GoalItem>> GetRootGoalsAsync(Guid userId);
    Task UpdateAsync(GoalItem goal);
    Task DeleteAsync(Guid id);
}
