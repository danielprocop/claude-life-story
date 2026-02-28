using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Models;

namespace DiarioIntelligente.Core.Interfaces;

public interface IClarificationService
{
    Task EvaluateEntryAsync(
        Guid userId,
        Entry entry,
        string? eventType,
        decimal? eventTotal,
        decimal? myShare,
        int participantCount,
        bool hasExplicitSettlement,
        CancellationToken cancellationToken = default);

    Task<List<ClarificationQuestionResponse>> GetOpenQuestionsAsync(Guid userId, int limit = 5, CancellationToken cancellationToken = default);
    Task<bool> AnswerAsync(Guid userId, Guid questionId, string answer, CancellationToken cancellationToken = default);
}
