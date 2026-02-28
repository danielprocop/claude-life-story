using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IEntryRepository
{
    Task<Entry> CreateAsync(Entry entry);
    Task<Entry?> GetByIdAsync(Guid id, Guid userId);
    Task<(List<Entry> Items, int TotalCount)> GetByUserAsync(Guid userId, int page, int pageSize);
    Task UpdateEmbeddingAsync(Guid entryId, string embeddingVector);
    Task<List<Entry>> GetEntriesWithEmbeddingsAsync(Guid userId);
    Task<List<Entry>> GetByDateRangeAsync(Guid userId, DateTime from, DateTime to);
    Task<int> CountByUserAsync(Guid userId);
    Task<List<Entry>> SearchAsync(Guid userId, string query, int limit);
}
