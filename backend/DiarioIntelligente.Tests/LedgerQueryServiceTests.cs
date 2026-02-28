using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DiarioIntelligente.Tests;

public class LedgerQueryServiceTests
{
    [Fact]
    public async Task Returns_Open_Debts_And_Monthly_MyShare()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var userId = Guid.NewGuid();
        var adiId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var eventEntityId = Guid.NewGuid();
        var sourceEntryId = Guid.NewGuid();
        var settlementEntryId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            db.Users.Add(new User { Id = userId, Email = "ledger@test.local", PasswordHash = "test" });
            db.CanonicalEntities.Add(new CanonicalEntity
            {
                Id = adiId,
                UserId = userId,
                Kind = "person",
                CanonicalName = "Adi",
                NormalizedCanonicalName = "adi",
                EntityCard = "Adi"
            });
            db.CanonicalEntities.Add(new CanonicalEntity
            {
                Id = eventEntityId,
                UserId = userId,
                Kind = "event",
                CanonicalName = "Cena test",
                NormalizedCanonicalName = "cenatest",
                EntityCard = "Cena test"
            });

            db.Entries.Add(new Entry { Id = sourceEntryId, UserId = userId, Content = "cena con Adi", CreatedAt = DateTime.UtcNow.AddDays(-1) });
            db.Entries.Add(new Entry { Id = settlementEntryId, UserId = userId, Content = "devo 50 ad Adi", CreatedAt = DateTime.UtcNow.AddDays(-1) });

            db.MemoryEvents.Add(new MemoryEvent
            {
                Id = eventId,
                UserId = userId,
                EntityId = eventEntityId,
                SourceEntryId = sourceEntryId,
                EventType = "cena",
                Title = "Cena test",
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                IncludesUser = true,
                Currency = "EUR",
                EventTotal = 100m,
                MyShare = 50m
            });

            db.Settlements.Add(new Settlement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventId = eventId,
                CounterpartyEntityId = adiId,
                SourceEntryId = settlementEntryId,
                Direction = "user_owes",
                OriginalAmount = 50m,
                RemainingAmount = 50m,
                Currency = "EUR",
                Status = "open"
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var service = new LedgerQueryService(db);
            var debts = await service.GetOpenDebtsAsync(userId);
            var byName = await service.GetOpenDebtForCounterpartyAsync(userId, "adi");
            var spending = await service.GetMySpendingAsync(userId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            var debt = Assert.Single(debts);
            Assert.Equal("Adi", debt.CounterpartyName);
            Assert.Equal(50m, debt.AmountOpen);
            Assert.NotNull(byName);
            Assert.Equal(50m, byName!.AmountOpen);
            Assert.Equal(50m, spending.Total);
        }
    }
}
