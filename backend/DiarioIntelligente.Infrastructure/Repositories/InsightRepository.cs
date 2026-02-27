using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class InsightRepository : IInsightRepository
{
    private readonly AppDbContext _db;

    public InsightRepository(AppDbContext db) => _db = db;

    public async Task<Insight> CreateAsync(Insight insight)
    {
        _db.Insights.Add(insight);
        await _db.SaveChangesAsync();
        return insight;
    }

    public async Task<List<Insight>> GetByUserAsync(Guid userId, int limit = 50)
    {
        return await _db.Insights
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.GeneratedAt)
            .Take(limit)
            .ToListAsync();
    }
}
