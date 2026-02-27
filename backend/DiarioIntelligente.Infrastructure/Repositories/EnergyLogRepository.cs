using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class EnergyLogRepository : IEnergyLogRepository
{
    private readonly AppDbContext _db;

    public EnergyLogRepository(AppDbContext db) => _db = db;

    public async Task<EnergyLog> CreateAsync(EnergyLog log)
    {
        _db.EnergyLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task<List<EnergyLog>> GetByUserAsync(Guid userId, int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        return await _db.EnergyLogs
            .Where(e => e.UserId == userId && e.RecordedAt >= from)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync();
    }

    public async Task<List<EnergyLog>> GetByDateRangeAsync(Guid userId, DateTime from, DateTime to)
    {
        return await _db.EnergyLogs
            .Where(e => e.UserId == userId && e.RecordedAt >= from && e.RecordedAt <= to)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync();
    }
}
