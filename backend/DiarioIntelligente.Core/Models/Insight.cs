namespace DiarioIntelligente.Core.Models;

public class Insight
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty; // weekly | goal_completed | pattern | anniversary

    public User User { get; set; } = null!;
}
