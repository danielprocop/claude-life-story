namespace DiarioIntelligente.Core.Models;

public class MemoryEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public Guid SourceEntryId { get; set; }
    public string EventType { get; set; } = string.Empty; // dinner | expense | outing | generic
    public string Title { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public bool IncludesUser { get; set; } = true;
    public string Currency { get; set; } = "EUR";
    public decimal? EventTotal { get; set; }
    public decimal? MyShare { get; set; }
    public string? Notes { get; set; }
    public string? SourceSnippet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public CanonicalEntity Entity { get; set; } = null!;
    public Entry SourceEntry { get; set; } = null!;
    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}
