namespace DiarioIntelligente.Core.Models;

public class PersonalPolicy
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PolicyKey { get; set; } = string.Empty;
    public string PolicyValue { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public string Origin { get; set; } = "inferred"; // explicit | inferred
    public string? Scope { get; set; } // e.g. category:dinner | person:<entityId>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
