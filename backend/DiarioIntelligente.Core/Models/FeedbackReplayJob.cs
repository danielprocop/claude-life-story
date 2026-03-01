namespace DiarioIntelligente.Core.Models;

public class FeedbackReplayJob
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "queued"; // queued | running | completed | failed
    public int PolicyVersion { get; set; }
    public Guid? TargetUserId { get; set; }
    public bool DryRun { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? SummaryJson { get; set; }
    public string? Error { get; set; }

    public User? TargetUser { get; set; }
}
