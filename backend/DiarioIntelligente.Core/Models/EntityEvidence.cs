namespace DiarioIntelligente.Core.Models;

public class EntityEvidence
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public Guid EntryId { get; set; }
    public string EvidenceType { get; set; } = string.Empty; // mention | name_assignment | alias_assignment | merge | role_anchor
    public string Snippet { get; set; } = string.Empty;
    public string? PropertyName { get; set; }
    public string? Value { get; set; }
    public string? MergeReason { get; set; }
    public float Confidence { get; set; } = 1.0f;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public CanonicalEntity Entity { get; set; } = null!;
    public Entry Entry { get; set; } = null!;
}
