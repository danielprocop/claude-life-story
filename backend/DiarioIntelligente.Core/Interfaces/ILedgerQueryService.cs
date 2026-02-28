using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface ILedgerQueryService
{
    Task<List<OpenDebtResponse>> GetOpenDebtsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OpenDebtResponse?> GetOpenDebtForCounterpartyAsync(Guid userId, string counterpartyName, CancellationToken cancellationToken = default);
    Task<SpendingSummaryResponse> GetMySpendingAsync(Guid userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<EventSpendingSummaryResponse> GetEventSpendingAsync(Guid userId, string eventType, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
