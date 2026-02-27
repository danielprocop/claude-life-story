using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IInsightRepository
{
    Task<Insight> CreateAsync(Insight insight);
    Task<List<Insight>> GetByUserAsync(Guid userId, int limit = 50);
}
