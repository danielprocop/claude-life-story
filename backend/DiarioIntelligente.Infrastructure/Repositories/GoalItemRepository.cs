using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class GoalItemRepository : IGoalItemRepository
{
    private readonly AppDbContext _db;

    public GoalItemRepository(AppDbContext db) => _db = db;

    public async Task<GoalItem> CreateAsync(GoalItem goal)
    {
        _db.GoalItems.Add(goal);
        await _db.SaveChangesAsync();
        return goal;
    }

    public async Task<GoalItem?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _db.GoalItems
            .Include(g => g.SubGoals)
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
    }

    public async Task<List<GoalItem>> GetRootGoalsAsync(Guid userId)
    {
        return await _db.GoalItems
            .Where(g => g.UserId == userId && g.ParentGoalId == null)
            .Include(g => g.SubGoals.OrderBy(s => s.SortOrder))
            .OrderBy(g => g.SortOrder)
            .ToListAsync();
    }

    public async Task UpdateAsync(GoalItem goal)
    {
        _db.GoalItems.Update(goal);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _db.GoalItems.Where(g => g.Id == id).ExecuteDeleteAsync();
    }
}
