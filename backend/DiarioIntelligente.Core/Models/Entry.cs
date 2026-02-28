namespace DiarioIntelligente.Core.Models;

public class Entry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? EmbeddingVector { get; set; }
    public bool HasPendingDerivedData => UpdatedAt != null && string.IsNullOrWhiteSpace(EmbeddingVector);

    public User User { get; set; } = null!;
    public ICollection<EntryConceptMap> EntryConceptMaps { get; set; } = new List<EntryConceptMap>();
    public EntryProcessingState? ProcessingState { get; set; }
}
