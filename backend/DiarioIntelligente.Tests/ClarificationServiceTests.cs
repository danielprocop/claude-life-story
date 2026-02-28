using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DiarioIntelligente.Tests;

public class ClarificationServiceTests
{
    [Fact]
    public async Task Creates_Question_And_Persists_Policy_After_Answer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new User { Id = userId, Email = "clarification@test.local", PasswordHash = "test" });
            db.Entries.Add(new Entry { Id = entryId, UserId = userId, Content = "cena", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var service = new ClarificationService(db);
            var entry = await db.Entries.SingleAsync(item => item.Id == entryId);

            await service.EvaluateEntryAsync(
                userId,
                entry,
                "cena",
                100m,
                50m,
                2,
                hasExplicitSettlement: false);

            var open = await service.GetOpenQuestionsAsync(userId);
            var question = Assert.Single(open);

            var answered = await service.AnswerAsync(userId, question.Id, "si, dividi uguale");
            Assert.True(answered);

            var policy = await db.PersonalPolicies.SingleAsync(item => item.UserId == userId);
            Assert.Equal("default_split_policy", policy.PolicyKey);
            Assert.Equal("equal", policy.PolicyValue);
            Assert.Equal("eventType:cena", policy.Scope);
        }
    }
}
