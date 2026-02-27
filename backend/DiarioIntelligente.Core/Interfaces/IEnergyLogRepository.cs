using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IEnergyLogRepository
{
    Task<EnergyLog> CreateAsync(EnergyLog log);
    Task<List<EnergyLog>> GetByUserAsync(Guid userId, int days = 30);
    Task<List<EnergyLog>> GetByDateRangeAsync(Guid userId, DateTime from, DateTime to);
}
