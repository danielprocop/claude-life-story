using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IChatMessageRepository
{
    Task<ChatMessage> CreateAsync(ChatMessage message);
    Task<List<ChatMessage>> GetRecentAsync(Guid userId, int limit = 20);
}
