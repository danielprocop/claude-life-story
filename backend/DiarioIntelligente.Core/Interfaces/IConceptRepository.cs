using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IConceptRepository
{
    Task<Concept?> FindByLabelAndTypeAsync(Guid userId, string label, string type);
    Task<Concept> CreateAsync(Concept concept);
    Task UpdateLastSeenAsync(Guid conceptId, DateTime lastSeen);
    Task<List<Concept>> GetByUserAsync(Guid userId);
    Task<List<Concept>> GetGoalsAndDesiresAsync(Guid userId);
    Task AddEntryConceptMapAsync(EntryConceptMap map);
}
