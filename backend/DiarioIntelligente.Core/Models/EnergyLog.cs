namespace DiarioIntelligente.Core.Models;

public class EnergyLog
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public Guid UserId { get; set; }
    public int EnergyLevel { get; set; } // 1-10
    public int StressLevel { get; set; } // 1-10
    public string? DominantEmotion { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Entry Entry { get; set; } = null!;
    public User User { get; set; } = null!;
}
