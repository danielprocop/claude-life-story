namespace DiarioIntelligente.Core.Models;

public class GoalItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentGoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "active"; // active | completed | abandoned
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int SortOrder { get; set; }

    public User User { get; set; } = null!;
    public GoalItem? ParentGoal { get; set; }
    public ICollection<GoalItem> SubGoals { get; set; } = new List<GoalItem>();
}
