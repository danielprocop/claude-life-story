namespace DiarioIntelligente.Core.Models;

public class SettlementPayment
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public Guid EntryId { get; set; }
    public decimal Amount { get; set; }
    public string? Snippet { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Settlement Settlement { get; set; } = null!;
    public Entry Entry { get; set; } = null!;
}
