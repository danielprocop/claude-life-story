namespace DiarioIntelligente.Core.DTOs;

public record CreateGoalRequest(string Title, string? Description, Guid? ParentGoalId);

public record UpdateGoalRequest(string? Title, string? Status);

public record GoalItemResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    Guid? ParentGoalId,
    List<GoalItemResponse> SubGoals
);
