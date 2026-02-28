using System.Text.Json;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : AuthenticatedController
{
    private readonly IAiService _aiService;
    private readonly IEntryRepository _entryRepo;
    private readonly IChatMessageRepository _chatRepo;

    public ChatController(IAiService aiService, IEntryRepository entryRepo, IChatMessageRepository chatRepo)
    {
        _aiService = aiService;
        _entryRepo = entryRepo;
        _chatRepo = chatRepo;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (!_aiService.IsConfigured)
            return Ok(new ChatResponse("L'AI non è configurata. Aggiungi una chiave API OpenAI.", new List<ChatSourceEntry>()));

        var userId = GetUserId();

        // Generate embedding for user message to find relevant entries
        var queryEmbedding = await _aiService.GetEmbeddingAsync(request.Message);
        var allEntries = await _entryRepo.GetEntriesWithEmbeddingsAsync(userId);

        // Find most relevant entries using cosine similarity
        var rankedEntries = allEntries
            .Where(e => !string.IsNullOrEmpty(e.EmbeddingVector))
            .Select(e =>
            {
                var entryEmbedding = JsonSerializer.Deserialize<float[]>(e.EmbeddingVector!);
                var similarity = entryEmbedding != null ? _aiService.CosineSimilarity(queryEmbedding, entryEmbedding) : 0f;
                return new { Entry = e, Similarity = similarity };
            })
            .Where(x => x.Similarity > 0.3f)
            .OrderByDescending(x => x.Similarity)
            .Take(10)
            .ToList();

        // Build context from relevant entries
        var entriesContext = string.Join("\n\n---\n\n", rankedEntries.Select(x =>
            $"[{x.Entry.CreatedAt:dd/MM/yyyy HH:mm}] (rilevanza: {x.Similarity:F2})\n{x.Entry.Content}"));

        // Get recent chat history
        var recentChat = await _chatRepo.GetRecentAsync(userId, 10);
        var chatHistory = string.Join("\n", recentChat.Select(m =>
            $"{(m.Role == "user" ? "Utente" : "Assistente")}: {m.Content}"));

        // Generate response
        var answer = await _aiService.ChatWithContextAsync(request.Message, entriesContext, chatHistory);

        // Save messages
        await _chatRepo.CreateAsync(new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = "user",
            Content = request.Message,
            CreatedAt = DateTime.UtcNow
        });
        await _chatRepo.CreateAsync(new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = "assistant",
            Content = answer,
            CreatedAt = DateTime.UtcNow
        });

        var sources = rankedEntries.Select(x => new ChatSourceEntry(
            x.Entry.Id,
            x.Entry.Content.Length > 100 ? x.Entry.Content[..100] + "..." : x.Entry.Content,
            x.Entry.CreatedAt,
            x.Similarity
        )).ToList();

        return Ok(new ChatResponse(answer, sources));
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<ChatHistoryItem>>> GetHistory()
    {
        var messages = await _chatRepo.GetRecentAsync(GetUserId(), 50);
        return Ok(messages.Select(m => new ChatHistoryItem(m.Role, m.Content, m.CreatedAt)).ToList());
    }
}
