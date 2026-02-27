namespace DiarioIntelligente.Core.Models;

public class Concept
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // person | place | desire | goal | activity | emotion
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<EntryConceptMap> EntryConceptMaps { get; set; } = new List<EntryConceptMap>();
    public ICollection<Connection> ConnectionsAsA { get; set; } = new List<Connection>();
    public ICollection<Connection> ConnectionsAsB { get; set; } = new List<Connection>();
}
