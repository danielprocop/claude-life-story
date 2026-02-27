namespace DiarioIntelligente.Core.Models;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty; // user | assistant
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
