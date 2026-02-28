namespace DiarioIntelligente.Core.Models;

public class ClarificationQuestion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? EntryId { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? ContextJson { get; set; }
    public string Status { get; set; } = "open"; // open | answered | dismissed
    public string? Answer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }

    public User User { get; set; } = null!;
    public Entry? Entry { get; set; }
}
