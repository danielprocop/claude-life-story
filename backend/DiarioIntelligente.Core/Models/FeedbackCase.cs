namespace DiarioIntelligente.Core.Models;

public class FeedbackCase
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByRole { get; set; } = "ANNOTATOR"; // ADMIN | DEV | ANNOTATOR
    public string ScopeDefault { get; set; } = "GLOBAL"; // GLOBAL | USER
    public string Status { get; set; } = "DRAFT"; // DRAFT | PREVIEWED | APPLIED | REVERTED
    public string TemplateId { get; set; } = string.Empty; // T1..T8
    public string TemplatePayloadJson { get; set; } = "{}";
    public string? ReferencesJson { get; set; }
    public string? PreviewSummaryJson { get; set; }
    public int? AppliedPolicyVersion { get; set; }
    public string? Reason { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<FeedbackAction> Actions { get; set; } = new List<FeedbackAction>();
}
