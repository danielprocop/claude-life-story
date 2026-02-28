namespace DiarioIntelligente.Core.DTOs;

public record OpenDebtResponse(
    Guid CounterpartyEntityId,
    string CounterpartyName,
    decimal AmountOpen,
    string Currency,
    int OpenItems
);

public record SpendingSummaryResponse(
    DateTime From,
    DateTime To,
    decimal Total,
    string Currency
);

public record EventSpendingSummaryResponse(
    string EventType,
    DateTime From,
    DateTime To,
    decimal TotalEventSpend,
    int EventCount,
    string Currency
);

public record ClarificationQuestionResponse(
    Guid Id,
    string QuestionType,
    string Prompt,
    DateTime CreatedAt
);

public record AnswerClarificationRequest(
    string Answer
);
