using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiarioIntelligente.Tests;

public class FeedbackSystemTests
{
    [Fact]
    public async Task G1_BlockToken_Prevents_Stopword_As_Person_And_Debug_Shows_Rule()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessEntryAsync(user.Id, "inoltre oggi mi sento stanco");

        await using (var db = fixture.CreateDbContext())
        {
            var pre = await db.CanonicalEntities
                .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "inoltre")
                .FirstOrDefaultAsync();

            if (pre == null)
            {
                db.CanonicalEntities.Add(new CanonicalEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Kind = "person",
                    CanonicalName = "Inoltre",
                    NormalizedCanonicalName = "inoltre",
                    EntityCard = "Inoltre"
                });
                await db.SaveChangesAsync();
            }
        }

        await fixture.ApplyFeedbackAsync(
            user.Id,
            new FeedbackCaseApplyRequest(
                "T1",
                Json("{" +
                     "\"token\":\"inoltre\"," +
                     "\"applies_to\":\"PERSON\"," +
                     "\"classification\":\"CONNECTIVE\"}"),
                null,
                "block stopword",
                null,
                user.Id,
                true));

        await fixture.ProcessEntryAsync(user.Id, "inoltre sono andato al parco");

        await using var verifyDb = fixture.CreateDbContext();
        var personInoltre = await verifyDb.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "inoltre" && x.Kind == "person")
            .CountAsync();
        Assert.Equal(0, personInoltre);

        var admin = fixture.CreateFeedbackAdminService(verifyDb);
        var reviewQueue = await admin.GetReviewQueueAsync(user.Id, 20);
        Assert.DoesNotContain(reviewQueue, x => x.IssueType == "STOPWORD_AS_PERSON");

        var suppressed = await verifyDb.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "inoltre")
            .Select(x => x.Id)
            .FirstAsync();

        var debug = await admin.GetEntityDebugAsync(user.Id, suppressed);
        Assert.NotNull(debug);
        Assert.Contains(debug!.RelevantActions, x => x.ActionType == "BLOCK_TOKEN_GLOBAL");
    }

    [Fact]
    public async Task G2_MergeEntities_Creates_Redirect_And_Hides_Duplicates()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        Guid aId;
        Guid bId;
        await using (var db = fixture.CreateDbContext())
        {
            var a = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Adi",
                NormalizedCanonicalName = "adi",
                EntityCard = "Adi"
            };
            var b = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Adi Fratello",
                NormalizedCanonicalName = "adifratello",
                EntityCard = "Adi Fratello"
            };
            db.CanonicalEntities.AddRange(a, b);
            db.EntityAliases.Add(new EntityAlias
            {
                Id = Guid.NewGuid(),
                EntityId = b.Id,
                Alias = "Adi(fratello)",
                NormalizedAlias = "adifratello",
                AliasType = "test",
                Confidence = 1
            });
            await db.SaveChangesAsync();
            aId = a.Id;
            bId = b.Id;
        }

        await fixture.ApplyFeedbackAsync(
            user.Id,
            new FeedbackCaseApplyRequest(
                "T3",
                Json("{" +
                     $"\"entity_a_id\":\"{aId}\"," +
                     $"\"entity_b_id\":\"{bId}\"," +
                     $"\"canonical_id\":\"{aId}\"," +
                     "\"migrate_alias\":true," +
                     "\"migrate_edges\":true," +
                     "\"migrate_evidence\":true," +
                     "\"reason\":\"merge duplicate\"}"),
                null,
                "merge duplicate",
                null,
                user.Id,
                true));

        await using var verifyDb = fixture.CreateDbContext();
        var redirect = await verifyDb.EntityRedirects.FirstOrDefaultAsync(x => x.OldEntityId == bId && x.CanonicalEntityId == aId && x.Active);
        Assert.NotNull(redirect);

        var admin = fixture.CreateFeedbackAdminService(verifyDb);
        var search = await admin.SearchEntitiesAsync(user.Id, "adi", 20);
        Assert.Contains(search, x => x.Id == aId);
        Assert.DoesNotContain(search, x => x.Id == bId);
    }

    [Fact]
    public async Task G3_ChangeType_Overrides_Entity_Kind_And_Is_Auditable()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        Guid entityId;

        await using (var db = fixture.CreateDbContext())
        {
            var entity = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Serenity",
                NormalizedCanonicalName = "serenity",
                EntityCard = "Serenity"
            };
            db.CanonicalEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
        }

        await fixture.ApplyFeedbackAsync(
            user.Id,
            new FeedbackCaseApplyRequest(
                "T4",
                Json("{" +
                     $"\"entity_id\":\"{entityId}\"," +
                     "\"new_type\":\"idea\"," +
                     "\"reason\":\"not a person\"}"),
                null,
                "change type",
                null,
                user.Id,
                true));

        await using var verifyDb = fixture.CreateDbContext();
        var entityAfter = await verifyDb.CanonicalEntities.FirstAsync(x => x.Id == entityId);
        Assert.Equal("idea", entityAfter.Kind);

        var admin = fixture.CreateFeedbackAdminService(verifyDb);
        var debug = await admin.GetEntityDebugAsync(user.Id, entityId);
        Assert.NotNull(debug);
        Assert.Contains(debug!.RelevantActions, x => x.ActionType == "ENTITY_TYPE_CORRECTION");
    }

    [Fact]
    public async Task G4_AddAlias_Links_New_Mention_Without_New_Entity()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        Guid feliciaId;

        await using (var db = fixture.CreateDbContext())
        {
            var felicia = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Felicia",
                NormalizedCanonicalName = "felicia",
                EntityCard = "Felicia"
            };
            db.CanonicalEntities.Add(felicia);
            await db.SaveChangesAsync();
            feliciaId = felicia.Id;
        }

        await fixture.ApplyFeedbackAsync(
            user.Id,
            new FeedbackCaseApplyRequest(
                "T5",
                Json("{" +
                     $"\"entity_id\":\"{feliciaId}\"," +
                     "\"alias\":\"Felia\"," +
                     "\"op\":\"ADD\"}"),
                null,
                "alias typo",
                null,
                user.Id,
                true));

        await fixture.ProcessEntryAsync(user.Id, "oggi Felia mi ha aiutato");

        await using var verifyDb = fixture.CreateDbContext();
        var allPersons = await verifyDb.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.Kind == "person")
            .ToListAsync();

        Assert.Single(allPersons);
        var policyService = new FeedbackPolicyService(verifyDb);
        var ruleset = await policyService.GetRulesetAsync(user.Id);
        Assert.True(ruleset.UserAliasMap.TryGetValue("felia", out var resolvedId));
        Assert.Equal(feliciaId, resolvedId);
    }

    [Fact]
    public async Task G5_Auditability_Shows_Redirect_Chain_And_Relevant_Actions()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        Guid sourceId;
        Guid targetId;

        await using (var db = fixture.CreateDbContext())
        {
            var source = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Adi old",
                NormalizedCanonicalName = "adiold",
                EntityCard = "Adi old"
            };
            var target = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Adi",
                NormalizedCanonicalName = "adi",
                EntityCard = "Adi"
            };
            db.CanonicalEntities.AddRange(source, target);
            await db.SaveChangesAsync();
            sourceId = source.Id;
            targetId = target.Id;
        }

        await fixture.ApplyFeedbackAsync(
            user.Id,
            new FeedbackCaseApplyRequest(
                "T3",
                Json("{" +
                     $"\"entity_a_id\":\"{targetId}\"," +
                     $"\"entity_b_id\":\"{sourceId}\"," +
                     $"\"canonical_id\":\"{targetId}\"," +
                     "\"migrate_alias\":true," +
                     "\"migrate_edges\":true," +
                     "\"migrate_evidence\":true," +
                     "\"reason\":\"audit merge\"}"),
                null,
                "audit merge",
                null,
                user.Id,
                true));

        await using var verifyDb = fixture.CreateDbContext();
        var admin = fixture.CreateFeedbackAdminService(verifyDb);
        var debug = await admin.GetEntityDebugAsync(user.Id, sourceId);

        Assert.NotNull(debug);
        Assert.Equal(targetId, debug!.CanonicalEntityId);
        Assert.True(debug.RedirectChain.Count >= 2);
        Assert.Contains(debug.RelevantActions, x => x.ActionType == "MERGE_ENTITIES");
    }

    [Fact]
    public async Task G6_ReplayJobs_Are_Listed_After_Apply()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        Guid entityId;

        await using (var db = fixture.CreateDbContext())
        {
            var entity = new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = "person",
                CanonicalName = "Test node",
                NormalizedCanonicalName = "testnode",
                EntityCard = "Test node"
            };
            db.CanonicalEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
        }

        var apply = await fixture.ApplyFeedbackAsync(
            user.Id,
            new FeedbackCaseApplyRequest(
                "T4",
                Json("{" +
                     $"\"entity_id\":\"{entityId}\"," +
                     "\"new_type\":\"idea\"," +
                     "\"reason\":\"queue check\"}"),
                null,
                "queue check",
                null,
                user.Id,
                true));

        await using var verifyDb = fixture.CreateDbContext();
        var admin = fixture.CreateFeedbackAdminService(verifyDb);
        var jobs = await admin.GetReplayJobsAsync(user.Id, null, 20);

        Assert.Contains(jobs, job => job.Id == apply.ReplayJob.Id);
    }

    private static System.Text.Json.JsonElement Json(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class InMemoryReplayScheduler : IFeedbackReplayScheduler
    {
        public List<Guid> JobIds { get; } = new();

        public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            JobIds.Add(jobId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly InMemoryReplayScheduler _replayScheduler = new();

        private TestFixture(SqliteConnection connection, DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            return new TestFixture(connection, options);
        }

        public AppDbContext CreateDbContext() => new(_options);

        public async Task<User> CreateUserAsync()
        {
            await using var db = CreateDbContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = "test",
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public async Task ProcessEntryAsync(Guid userId, string content)
        {
            await using var db = CreateDbContext();
            var policyService = new FeedbackPolicyService(db);
            var graph = CreateGraphService(db, policyService);

            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            db.Entries.Add(entry);
            await db.SaveChangesAsync();

            var ruleset = await policyService.GetRulesetAsync(userId);
            await graph.ProcessEntryAsync(entry, new AiAnalysisResult(), ruleset);
        }

        public async Task<FeedbackApplyResponse> ApplyFeedbackAsync(Guid actorUserId, FeedbackCaseApplyRequest request)
        {
            await using var db = CreateDbContext();
            var policyService = new FeedbackPolicyService(db);
            var adminService = CreateFeedbackAdminService(db, policyService);
            return await adminService.ApplyCaseAsync(actorUserId, "DEV", request);
        }

        public FeedbackAdminService CreateFeedbackAdminService(AppDbContext db)
        {
            var policyService = new FeedbackPolicyService(db);
            return CreateFeedbackAdminService(db, policyService);
        }

        private FeedbackAdminService CreateFeedbackAdminService(AppDbContext db, IFeedbackPolicyService policyService)
        {
            var graph = CreateGraphService(db, policyService);
            return new FeedbackAdminService(
                db,
                policyService,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                graph,
                _replayScheduler,
                new NullLogger<FeedbackAdminService>());
        }

        private static CognitiveGraphService CreateGraphService(AppDbContext db, IFeedbackPolicyService policyService)
        {
            return new CognitiveGraphService(
                db,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                new NoOpEntityRetrievalService(),
                policyService,
                new ClarificationService(db),
                new NullLogger<CognitiveGraphService>());
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
