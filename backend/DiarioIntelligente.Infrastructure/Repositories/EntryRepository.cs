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

    public async Task<List<Entry>> GetAllByUserAsync(Guid userId)
    {
        return await _db.Entries
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
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

    public async Task<Entry> UpdateAsync(Entry entry)
    {
        _db.Entries.Update(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var entry = await _db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
        if (entry == null)
            return false;

        _db.Entries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
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

    public async Task<List<Entry>> SearchAsync(Guid userId, string query, int limit)
    {
        var normalizedQuery = query.Trim().ToLower();

        return await _db.Entries
            .Where(e => e.UserId == userId && e.Content.ToLower().Contains(normalizedQuery))
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Include(e => e.EntryConceptMaps)
            .ToListAsync();
    }

    public async Task<List<(Entry Entry, int SharedConceptCount)>> GetRelatedAsync(Guid entryId, Guid userId, int limit)
    {
        var targetConceptIds = await _db.EntryConceptMaps
            .Where(m => m.EntryId == entryId)
            .Select(m => m.ConceptId)
            .ToListAsync();

        if (targetConceptIds.Count == 0)
            return new List<(Entry Entry, int SharedConceptCount)>();

        var candidates = await _db.Entries
            .Where(e => e.UserId == userId && e.Id != entryId)
            .Include(e => e.EntryConceptMaps)
            .OrderByDescending(e => e.CreatedAt)
            .Take(200)
            .ToListAsync();

        return candidates
            .Select(entry => (
                Entry: entry,
                SharedConceptCount: entry.EntryConceptMaps.Count(m => targetConceptIds.Contains(m.ConceptId))
            ))
            .Where(x => x.SharedConceptCount > 0)
            .OrderByDescending(x => x.SharedConceptCount)
            .ThenByDescending(x => x.Entry.CreatedAt)
            .Take(limit)
            .ToList();
    }
}
