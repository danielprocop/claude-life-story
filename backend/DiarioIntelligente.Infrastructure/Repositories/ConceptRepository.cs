using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class ConceptRepository : IConceptRepository
{
    private readonly AppDbContext _db;

    public ConceptRepository(AppDbContext db) => _db = db;

    public async Task<Concept?> FindByLabelAndTypeAsync(Guid userId, string label, string type)
    {
        return await _db.Concepts
            .FirstOrDefaultAsync(c => c.UserId == userId
                && c.Label.ToLower() == label.ToLower()
                && c.Type == type);
    }

    public async Task<Concept> CreateAsync(Concept concept)
    {
        _db.Concepts.Add(concept);
        await _db.SaveChangesAsync();
        return concept;
    }

    public async Task UpdateLastSeenAsync(Guid conceptId, DateTime lastSeen)
    {
        await _db.Concepts
            .Where(c => c.Id == conceptId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastSeenAt, lastSeen));
    }

    public async Task<List<Concept>> GetByUserAsync(Guid userId)
    {
        return await _db.Concepts
            .Where(c => c.UserId == userId)
            .Include(c => c.EntryConceptMaps)
            .OrderByDescending(c => c.LastSeenAt)
            .ToListAsync();
    }

    public async Task<List<Concept>> GetGoalsAndDesiresAsync(Guid userId)
    {
        return await _db.Concepts
            .Where(c => c.UserId == userId && (c.Type == "desire" || c.Type == "goal"))
            .Include(c => c.EntryConceptMaps)
                .ThenInclude(m => m.Entry)
            .OrderByDescending(c => c.LastSeenAt)
            .ToListAsync();
    }

    public async Task AddEntryConceptMapAsync(EntryConceptMap map)
    {
        _db.EntryConceptMaps.Add(map);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Concept>> SearchAsync(Guid userId, string query, int limit)
    {
        var normalizedQuery = query.Trim().ToLower();

        return await _db.Concepts
            .Where(c => c.UserId == userId &&
                (c.Label.ToLower().Contains(normalizedQuery) || c.Type.ToLower().Contains(normalizedQuery)))
            .Include(c => c.EntryConceptMaps)
            .OrderByDescending(c => c.LastSeenAt)
            .Take(limit)
            .ToListAsync();
    }
}
