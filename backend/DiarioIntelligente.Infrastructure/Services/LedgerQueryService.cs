using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class LedgerQueryService : ILedgerQueryService
{
    private readonly AppDbContext _db;

    public LedgerQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<OpenDebtResponse>> GetOpenDebtsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var openItems = await _db.Settlements
            .Where(item => item.UserId == userId && item.Direction == "user_owes" && item.Status != "settled")
            .Include(item => item.CounterpartyEntity)
            .ToListAsync(cancellationToken);

        return openItems
            .GroupBy(item => new
            {
                item.CounterpartyEntityId,
                item.CounterpartyEntity.CanonicalName,
                item.Currency
            })
            .Select(group => new OpenDebtResponse(
                group.Key.CounterpartyEntityId,
                group.Key.CanonicalName,
                group.Sum(item => item.RemainingAmount),
                group.Key.Currency,
                group.Count()))
            .OrderByDescending(item => item.AmountOpen)
            .ToList();
    }

    public async Task<OpenDebtResponse?> GetOpenDebtForCounterpartyAsync(Guid userId, string counterpartyName, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(counterpartyName);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var items = await _db.Settlements
            .Where(item => item.UserId == userId && item.Direction == "user_owes" && item.Status != "settled")
            .Include(item => item.CounterpartyEntity)
            .ThenInclude(entity => entity.Aliases)
            .ToListAsync(cancellationToken);

        var matching = items
            .Where(item =>
                Normalize(item.CounterpartyEntity.CanonicalName) == normalized ||
                item.CounterpartyEntity.Aliases.Any(alias => alias.NormalizedAlias == normalized))
            .ToList();

        if (matching.Count == 0)
            return null;

        var currency = matching[0].Currency;
        return new OpenDebtResponse(
            matching[0].CounterpartyEntityId,
            matching[0].CounterpartyEntity.CanonicalName,
            matching.Sum(item => item.RemainingAmount),
            currency,
            matching.Count);
    }

    public async Task<SpendingSummaryResponse> GetMySpendingAsync(Guid userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var events = await _db.MemoryEvents
            .Where(item =>
                item.UserId == userId &&
                item.OccurredAt >= from &&
                item.OccurredAt <= to &&
                item.MyShare.HasValue)
            .ToListAsync(cancellationToken);

        var sum = events.Sum(item => item.MyShare ?? 0m);

        return new SpendingSummaryResponse(from, to, sum, "EUR");
    }

    public async Task<EventSpendingSummaryResponse> GetEventSpendingAsync(
        Guid userId,
        string eventType,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = eventType.Trim().ToLowerInvariant();
        var events = await _db.MemoryEvents
            .Where(item =>
                item.UserId == userId &&
                item.EventType == normalizedType &&
                item.OccurredAt >= from &&
                item.OccurredAt <= to &&
                item.EventTotal.HasValue)
            .ToListAsync(cancellationToken);

        return new EventSpendingSummaryResponse(
            normalizedType,
            from,
            to,
            events.Sum(item => item.EventTotal ?? 0m),
            events.Count,
            "EUR");
    }

    private static string Normalize(string raw)
    {
        var cleaned = raw.Trim().ToLowerInvariant();
        return new string(cleaned.Where(char.IsLetterOrDigit).ToArray());
    }
}
