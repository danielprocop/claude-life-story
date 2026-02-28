namespace DiarioIntelligente.Core.Models;

public class EventParticipant
{
    public Guid EventId { get; set; }
    public Guid EntityId { get; set; }
    public string Role { get; set; } = "participant"; // participant | payer

    public MemoryEvent Event { get; set; } = null!;
    public CanonicalEntity Entity { get; set; } = null!;
}
