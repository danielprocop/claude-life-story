namespace DiarioIntelligente.Core.Models;

public class EntityRedirect
{
    public Guid OldEntityId { get; set; }
    public Guid CanonicalEntityId { get; set; }
    public Guid CreatedByActionId { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CanonicalEntity OldEntity { get; set; } = null!;
    public CanonicalEntity CanonicalEntity { get; set; } = null!;
    public FeedbackAction CreatedByAction { get; set; } = null!;
}
