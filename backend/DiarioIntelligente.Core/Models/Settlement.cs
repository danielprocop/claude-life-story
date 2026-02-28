namespace DiarioIntelligente.Core.Models;

public class Settlement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? EventId { get; set; }
    public Guid CounterpartyEntityId { get; set; }
    public Guid SourceEntryId { get; set; }
    public string Direction { get; set; } = string.Empty; // user_owes | owes_user
    public decimal OriginalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = "open"; // open | partially_paid | settled
    public string? Notes { get; set; }
    public string? SourceSnippet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public MemoryEvent? Event { get; set; }
    public CanonicalEntity CounterpartyEntity { get; set; } = null!;
    public Entry SourceEntry { get; set; } = null!;
    public ICollection<SettlementPayment> Payments { get; set; } = new List<SettlementPayment>();
}
