namespace DiarioIntelligente.Core.Models;

public class Connection
{
    public Guid ConceptAId { get; set; }
    public Guid ConceptBId { get; set; }
    public float Strength { get; set; }
    public string Type { get; set; } = string.Empty;

    public Concept ConceptA { get; set; } = null!;
    public Concept ConceptB { get; set; } = null!;
}
