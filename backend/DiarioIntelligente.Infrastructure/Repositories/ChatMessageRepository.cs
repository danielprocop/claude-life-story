using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly AppDbContext _db;

    public ChatMessageRepository(AppDbContext db) => _db = db;

    public async Task<ChatMessage> CreateAsync(ChatMessage message)
    {
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    public async Task<List<ChatMessage>> GetRecentAsync(Guid userId, int limit = 20)
    {
        return await _db.ChatMessages
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}
