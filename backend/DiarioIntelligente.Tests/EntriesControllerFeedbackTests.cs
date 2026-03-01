using DiarioIntelligente.API.Controllers;
using DiarioIntelligente.API.Services;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Repositories;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiarioIntelligente.Tests;

public class EntriesControllerFeedbackTests
{
    [Fact]
    public async Task SubmitEntityFeedback_Persists_Override_And_Queues_Rebuild()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        await using (var seedDb = new AppDbContext(options))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.Users.Add(new User
            {
                Id = userId,
                Email = "feedback@test.local",
                PasswordHash = "test"
            });
            seedDb.Entries.Add(new Entry
            {
                Id = entryId,
                UserId = userId,
                Content = "alle 12:30 oggi giochera il Milan.",
                CreatedAt = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = new AppDbContext(options);
        var entryRepo = new EntryRepository(db);
        var rebuildQueue = new UserMemoryRebuildQueue();
        var controller = new EntriesController(
            entryRepo,
            new EntryProcessingQueue(),
            rebuildQueue,
            new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
            db);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[CurrentUserContext.HttpContextItemKey] =
            new CurrentUserContext(userId, "feedback@test.local", true);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var action = await controller.SubmitEntityFeedback(
            entryId,
            new EntryEntityFeedbackRequest("Milan", "team", "squadra di calcio"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<EntryEntityFeedbackResponse>(ok.Value);
        Assert.Equal("team", payload.AppliedKind);
        Assert.True(payload.RebuildQueued);

        var overridePolicy = await db.PersonalPolicies.SingleAsync(policy => policy.PolicyKey == "entity_kind_override");
        Assert.Equal("team", overridePolicy.PolicyValue);
        Assert.Equal("name:milan", overridePolicy.Scope);

        var notePolicy = await db.PersonalPolicies.SingleAsync(policy => policy.PolicyKey == "entity_feedback_note");
        Assert.Equal("entry:" + entryId + ":name:milan", notePolicy.Scope);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var queuedUserId = await rebuildQueue.DequeueAsync(cts.Token);
        Assert.Equal(userId, queuedUserId);
    }
}
