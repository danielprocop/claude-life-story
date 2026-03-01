namespace DiarioIntelligente.Core.Models;

public class FeedbackAction
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Scope { get; set; } = "GLOBAL"; // GLOBAL | USER
    public Guid? TargetUserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | REVERTED
    public int PolicyVersion { get; set; }
    public Guid? SupersedesActionId { get; set; }

    public FeedbackCase Case { get; set; } = null!;
    public User? TargetUser { get; set; }
    public FeedbackAction? SupersedesAction { get; set; }
}
