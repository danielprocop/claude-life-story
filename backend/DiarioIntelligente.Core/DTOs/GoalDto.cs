namespace DiarioIntelligente.Core.DTOs;

public record GoalResponse(
    Guid Id,
    string Label,
    string Status, // in_progress | achieved | abandoned
    DateTime FirstSeenAt,
    DateTime? AchievedAt,
    List<GoalTimelineEntry> Timeline
);

public record GoalTimelineEntry(
    DateTime Date,
    string EntryPreview,
    string Signal // desire | progress | completion
);
