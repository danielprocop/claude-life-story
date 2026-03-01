namespace DiarioIntelligente.Core.Models;

public class PolicyVersion
{
    public int Version { get; set; } // PK
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public string? SummaryJson { get; set; }
    public string? Fingerprint { get; set; }

    public User CreatedByUser { get; set; } = null!;
}
