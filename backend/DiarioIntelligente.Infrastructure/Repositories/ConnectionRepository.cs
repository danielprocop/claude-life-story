using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class ConnectionRepository : IConnectionRepository
{
    private readonly AppDbContext _db;

    public ConnectionRepository(AppDbContext db) => _db = db;

    public async Task<Connection?> GetAsync(Guid conceptAId, Guid conceptBId)
    {
        return await _db.Connections
            .FirstOrDefaultAsync(c =>
                (c.ConceptAId == conceptAId && c.ConceptBId == conceptBId) ||
                (c.ConceptAId == conceptBId && c.ConceptBId == conceptAId));
    }

    public async Task CreateAsync(Connection connection)
    {
        _db.Connections.Add(connection);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStrengthAsync(Guid conceptAId, Guid conceptBId, float strength)
    {
        await _db.Connections
            .Where(c =>
                (c.ConceptAId == conceptAId && c.ConceptBId == conceptBId) ||
                (c.ConceptAId == conceptBId && c.ConceptBId == conceptAId))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Strength, strength));
    }

    public async Task<List<Connection>> GetByUserConceptsAsync(Guid userId)
    {
        var userConceptIds = await _db.Concepts
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .ToListAsync();

        return await _db.Connections
            .Where(c => userConceptIds.Contains(c.ConceptAId))
            .Include(c => c.ConceptA)
            .Include(c => c.ConceptB)
            .ToListAsync();
    }
}
