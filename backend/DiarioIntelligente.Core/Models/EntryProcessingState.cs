namespace DiarioIntelligente.Core.Models;

public class EntryProcessingState
{
    public Guid EntryId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? SourceUpdatedAt { get; set; }
    public DateTime LastProcessedAt { get; set; } = DateTime.UtcNow;
    public bool UsedAiAnalysis { get; set; }

    public Entry Entry { get; set; } = null!;
    public User User { get; set; } = null!;
}
