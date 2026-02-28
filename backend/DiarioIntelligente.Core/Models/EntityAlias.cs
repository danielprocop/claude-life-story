namespace DiarioIntelligente.Core.Models;

public class EntityAlias
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string NormalizedAlias { get; set; } = string.Empty;
    public string AliasType { get; set; } = string.Empty; // canonical_name | role_phrase | observed_name | observed_typo
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CanonicalEntity Entity { get; set; } = null!;
}
