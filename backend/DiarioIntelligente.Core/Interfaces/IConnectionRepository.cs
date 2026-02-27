using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IConnectionRepository
{
    Task<Connection?> GetAsync(Guid conceptAId, Guid conceptBId);
    Task CreateAsync(Connection connection);
    Task UpdateStrengthAsync(Guid conceptAId, Guid conceptBId, float strength);
    Task<List<Connection>> GetByUserConceptsAsync(Guid userId);
}
