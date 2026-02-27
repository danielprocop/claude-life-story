namespace DiarioIntelligente.Core.Models;

public class EntryConceptMap
{
    public Guid EntryId { get; set; }
    public Guid ConceptId { get; set; }
    public float Relevance { get; set; }

    public Entry Entry { get; set; } = null!;
    public Concept Concept { get; set; } = null!;
}
