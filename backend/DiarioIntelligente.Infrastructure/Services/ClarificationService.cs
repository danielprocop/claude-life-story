using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class ClarificationService : IClarificationService
{
    private readonly AppDbContext _db;

    public ClarificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EvaluateEntryAsync(
        Guid userId,
        Entry entry,
        string? eventType,
        decimal? eventTotal,
        decimal? myShare,
        int participantCount,
        bool hasExplicitSettlement,
        CancellationToken cancellationToken = default)
    {
        if (hasExplicitSettlement || string.IsNullOrWhiteSpace(eventType) || !eventTotal.HasValue || participantCount < 2 || !myShare.HasValue)
            return;

        var scope = $"eventType:{eventType.ToLowerInvariant()}";
        var existingPolicy = await _db.PersonalPolicies
            .FirstOrDefaultAsync(policy => policy.UserId == userId && policy.PolicyKey == "default_split_policy" && policy.Scope == scope, cancellationToken);

        if (existingPolicy != null)
            return;

        var openQuestionExists = await _db.ClarificationQuestions.AnyAsync(question =>
            question.UserId == userId &&
            question.QuestionType == "split_policy" &&
            question.Status == "open" &&
            question.CreatedAt >= DateTime.UtcNow.AddDays(-7), cancellationToken);

        if (openQuestionExists)
            return;

        var prompt = $"Quando registri una {eventType}, se non specifichi lo split devo assumere divisione uguale tra partecipanti?";
        var context = JsonSerializer.Serialize(new
        {
            eventType,
            eventTotal,
            myShare,
            participantCount
        });

        _db.ClarificationQuestions.Add(new ClarificationQuestion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EntryId = entry.Id,
            QuestionType = "split_policy",
            Prompt = prompt,
            ContextJson = context,
            Status = "open",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ClarificationQuestionResponse>> GetOpenQuestionsAsync(Guid userId, int limit = 5, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 20);
        return await _db.ClarificationQuestions
            .Where(question => question.UserId == userId && question.Status == "open")
            .OrderBy(question => question.CreatedAt)
            .Take(safeLimit)
            .Select(question => new ClarificationQuestionResponse(
                question.Id,
                question.QuestionType,
                question.Prompt,
                question.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnswerAsync(Guid userId, Guid questionId, string answer, CancellationToken cancellationToken = default)
    {
        var question = await _db.ClarificationQuestions
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Id == questionId && item.Status == "open", cancellationToken);

        if (question == null)
            return false;

        question.Answer = answer.Trim();
        question.AnsweredAt = DateTime.UtcNow;
        question.Status = "answered";

        if (question.QuestionType == "split_policy")
        {
            var value = NormalizeSplitPolicyValue(answer);
            var scope = ParseScopeFromContext(question.ContextJson);
            if (!string.IsNullOrWhiteSpace(scope) && !string.IsNullOrWhiteSpace(value))
            {
                var existing = await _db.PersonalPolicies.FirstOrDefaultAsync(policy =>
                    policy.UserId == userId &&
                    policy.PolicyKey == "default_split_policy" &&
                    policy.Scope == scope, cancellationToken);

                if (existing == null)
                {
                    _db.PersonalPolicies.Add(new PersonalPolicy
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        PolicyKey = "default_split_policy",
                        PolicyValue = value,
                        Scope = scope,
                        Origin = "explicit",
                        Confidence = 1.0f,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.PolicyValue = value;
                    existing.Origin = "explicit";
                    existing.Confidence = 1.0f;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? ParseScopeFromContext(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            if (!document.RootElement.TryGetProperty("eventType", out var eventTypeNode))
                return null;

            var eventType = eventTypeNode.GetString()?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(eventType))
                return null;

            return $"eventType:{eventType}";
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeSplitPolicyValue(string answer)
    {
        var normalized = answer.ToLowerInvariant();
        if (normalized.Contains("ugual") || normalized.Contains("meta") || normalized.Contains("meta") || normalized.Contains("half"))
            return "equal";

        var percentageMatch = Regex.Match(normalized, @"(?<value>\d{1,3})(?:\s*%)");
        if (percentageMatch.Success && decimal.TryParse(percentageMatch.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
            return $"percentage:{percent}";

        var numeric = Regex.Match(normalized, @"(?<value>\d+(?:[.,]\d+)?)");
        if (numeric.Success && decimal.TryParse(numeric.Groups["value"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return $"fixed:{amount}";

        return "equal";
    }
}
