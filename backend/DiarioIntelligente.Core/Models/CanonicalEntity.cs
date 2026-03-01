namespace DiarioIntelligente.Core.Models;

public class CanonicalEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = string.Empty; // person | event | goal | place | team | organization | project | activity | emotion | idea | problem | finance | object | vehicle | brand | product_model | generic
    public string CanonicalName { get; set; } = string.Empty;
    public string NormalizedCanonicalName { get; set; } = string.Empty;
    public string? AnchorKey { get; set; } // e.g. mother_of_user, brother_of_user
    public string EntityCard { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<EntityAlias> Aliases { get; set; } = new List<EntityAlias>();
    public ICollection<EntityEvidence> Evidence { get; set; } = new List<EntityEvidence>();
    public ICollection<EventParticipant> EventParticipants { get; set; } = new List<EventParticipant>();
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}
