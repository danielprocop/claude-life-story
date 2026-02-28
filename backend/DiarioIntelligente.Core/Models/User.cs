namespace DiarioIntelligente.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Settings { get; set; }

    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
    public ICollection<Concept> Concepts { get; set; } = new List<Concept>();
    public ICollection<Insight> Insights { get; set; } = new List<Insight>();
    public ICollection<EnergyLog> EnergyLogs { get; set; } = new List<EnergyLog>();
    public ICollection<GoalItem> GoalItems { get; set; } = new List<GoalItem>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public ICollection<CanonicalEntity> CanonicalEntities { get; set; } = new List<CanonicalEntity>();
    public ICollection<MemoryEvent> MemoryEvents { get; set; } = new List<MemoryEvent>();
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
    public ICollection<PersonalPolicy> PersonalPolicies { get; set; } = new List<PersonalPolicy>();
    public ICollection<ClarificationQuestion> ClarificationQuestions { get; set; } = new List<ClarificationQuestion>();
    public ICollection<EntryProcessingState> EntryProcessingStates { get; set; } = new List<EntryProcessingState>();
}
