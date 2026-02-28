using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DiarioIntelligente.Tests;

public class PersonalModelServiceTests
{
    [Fact]
    public async Task BuildAsync_Returns_Context_And_MicroSteps_From_User_Data()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "profile@test.local",
                PasswordHash = "test"
            };
            db.Users.Add(user);

            db.Entries.AddRange(
                new Entry { Id = Guid.NewGuid(), UserId = userId, Content = "oggi lavoro sul progetto e poi tempo con mia madre", CreatedAt = DateTime.UtcNow.AddHours(-12) },
                new Entry { Id = Guid.NewGuid(), UserId = userId, Content = "devo migliorare focus e priorita", CreatedAt = DateTime.UtcNow.AddHours(-3) });

            db.CanonicalEntities.Add(new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = "person",
                CanonicalName = "Felicia",
                NormalizedCanonicalName = "felicia",
                AnchorKey = "mother_of_user",
                EntityCard = "Felicia | mother_of_user"
            });

            var goal = new GoalItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Lanciare nuovo modulo",
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };
            db.GoalItems.Add(goal);

            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var service = new PersonalModelService(db);
            var profile = await service.BuildAsync(db.Users.Select(x => x.Id).Single());

            Assert.NotEmpty(profile.ContextSummary);
            Assert.True(profile.EntriesAnalyzed >= 2);
            Assert.True(profile.CanonicalEntities >= 1);
            Assert.True(profile.ActiveGoals >= 1);
            Assert.NotEmpty(profile.SuggestedMicroSteps);
        }
    }
}
