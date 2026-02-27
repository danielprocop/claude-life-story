using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class EntryRepository : IEntryRepository
{
    private readonly AppDbContext _db;

    public EntryRepository(AppDbContext db) => _db = db;

    public async Task<Entry> CreateAsync(Entry entry)
    {
        _db.Entries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<Entry?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _db.Entries
            .Include(e => e.EntryConceptMaps)
                .ThenInclude(m => m.Concept)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
    }

    public async Task<(List<Entry> Items, int TotalCount)> GetByUserAsync(Guid userId, int page, int pageSize)
    {
        var query = _db.Entries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(e => e.EntryConceptMaps)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task UpdateEmbeddingAsync(Guid entryId, string embeddingVector)
    {
        await _db.Entries
            .Where(e => e.Id == entryId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.EmbeddingVector, embeddingVector));
    }

    public async Task<List<Entry>> GetEntriesWithEmbeddingsAsync(Guid userId)
    {
        return await _db.Entries
            .Where(e => e.UserId == userId && e.EmbeddingVector != null)
            .OrderByDescending(e => e.CreatedAt)
            .Take(200) // limit for performance
            .ToListAsync();
    }

    public async Task<List<Entry>> GetByDateRangeAsync(Guid userId, DateTime from, DateTime to)
    {
        return await _db.Entries
            .Where(e => e.UserId == userId && e.CreatedAt >= from && e.CreatedAt <= to)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> CountByUserAsync(Guid userId)
    {
        return await _db.Entries.CountAsync(e => e.UserId == userId);
    }
}
