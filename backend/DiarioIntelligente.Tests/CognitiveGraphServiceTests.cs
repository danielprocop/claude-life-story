using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiarioIntelligente.Tests;

public class CognitiveGraphServiceTests
{
    [Fact]
    public async Task Merges_Mother_Felicia_And_Felia_Into_One_Entity()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "le mie figlie sono da mia madre");
        await fixture.ProcessAsync(user.Id, "mia madre si chiama Felicia");
        await fixture.ProcessAsync(user.Id, "oggi Felia ha preso le bambine");

        await using var db = fixture.CreateDbContext();
        var entities = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.AnchorKey == "mother_of_user")
            .Include(x => x.Aliases)
            .Include(x => x.Evidence)
            .ToListAsync();

        Assert.Single(entities);
        var mother = entities[0];
        Assert.Equal("Felicia", mother.CanonicalName);
        Assert.Contains(mother.Aliases, x => x.NormalizedAlias == "miamadre");
        Assert.Contains(mother.Aliases, x => x.NormalizedAlias == "felia");
        Assert.True(mother.Evidence.Count >= 3);
    }

    [Fact]
    public async Task Merges_Mother_With_Typo_SiChima_And_Lowercase_Name()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "le mie figlie oggi sono da mia madre");
        await fixture.ProcessAsync(user.Id, "mia madre si chima felicia");
        await fixture.ProcessAsync(user.Id, "felia mi ha scritto");

        await using var db = fixture.CreateDbContext();
        var entities = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.AnchorKey == "mother_of_user")
            .Include(x => x.Aliases)
            .ToListAsync();

        var mother = Assert.Single(entities);
        Assert.Equal("Felicia", mother.CanonicalName);
        Assert.Contains(mother.Aliases, x => x.NormalizedAlias == "felia");
    }

    [Fact]
    public async Task Resolves_Adi_Fratello_To_Single_Person_Node()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "oggi ho visto Adi(fratello) e mio fratello era tranquillo");

        await using var db = fixture.CreateDbContext();
        var entities = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.AnchorKey == "brother_of_user")
            .Include(x => x.Aliases)
            .ToListAsync();

        Assert.Single(entities);
        var brother = entities[0];
        Assert.Equal("Adi", brother.CanonicalName);
        Assert.Contains(brother.Aliases, x => x.NormalizedAlias == "adi");
        Assert.Contains(brother.Aliases, x => x.NormalizedAlias == "miofratello");
    }

    [Fact]
    public async Task Creates_Event_And_Settlement_With_Correct_MyShare()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "sono andato a cena con Adi(fratello) e ho speso 100 euro e devo dargli 50 perche ha pagato lui");

        await using var db = fixture.CreateDbContext();
        var memoryEvents = await db.MemoryEvents
            .Where(x => x.UserId == user.Id)
            .Include(x => x.Participants)
            .ThenInclude(x => x.Entity)
            .ToListAsync();

        var memoryEvent = Assert.Single(memoryEvents);

        var settlement = await db.Settlements
            .Where(x => x.UserId == user.Id)
            .Include(x => x.CounterpartyEntity)
            .SingleAsync();

        Assert.Equal("cena", memoryEvent.EventType);
        Assert.Equal(100m, memoryEvent.EventTotal);
        Assert.Equal(50m, memoryEvent.MyShare);
        Assert.Single(memoryEvent.Participants);
        Assert.Equal("Adi", memoryEvent.Participants.Single().Entity.CanonicalName);

        Assert.Equal("user_owes", settlement.Direction);
        Assert.Equal(50m, settlement.OriginalAmount);
        Assert.Equal(50m, settlement.RemainingAmount);
        Assert.Equal("open", settlement.Status);
        Assert.Equal("Adi", settlement.CounterpartyEntity.CanonicalName);
    }

    [Fact]
    public async Task Payment_Closes_Open_Settlement()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "sono andato a cena con Adi(fratello) e ho speso 100 euro e devo dargli 50 perche ha pagato lui");
        await fixture.ProcessAsync(user.Id, "ho dato 50 ad Adi");

        await using var db = fixture.CreateDbContext();
        var settlement = await db.Settlements
            .Where(x => x.UserId == user.Id)
            .Include(x => x.Payments)
            .SingleAsync();

        Assert.Equal(0m, settlement.RemainingAmount);
        Assert.Equal("settled", settlement.Status);
        Assert.Single(settlement.Payments);
        Assert.Equal(50m, settlement.Payments.Single().Amount);
    }

    [Fact]
    public async Task Explicit_Debt_Target_Is_Resolved_Without_With_Participant_Clause()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "ho visto Adi(fratello) oggi");
        await fixture.ProcessAsync(user.Id, "devo 50 ad Adi");

        await using var db = fixture.CreateDbContext();
        var settlement = await db.Settlements
            .Where(x => x.UserId == user.Id)
            .Include(x => x.CounterpartyEntity)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(settlement);
        Assert.Equal("Adi", settlement!.CounterpartyEntity.CanonicalName);
        Assert.Equal(50m, settlement.OriginalAmount);
        Assert.Equal("user_owes", settlement.Direction);
    }

    [Fact]
    public async Task Payment_Matches_Settlement_By_Amount_Not_Just_Latest()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "cena con Adi(fratello), devo dargli 30 perche ha pagato lui");
        await fixture.ProcessAsync(user.Id, "nuova cena con Adi, devo dargli 50 perche ha pagato lui");
        await fixture.ProcessAsync(user.Id, "ho dato 30 ad Adi");

        await using var db = fixture.CreateDbContext();
        var settlements = (await db.Settlements
            .Where(x => x.UserId == user.Id)
            .ToListAsync())
            .OrderBy(x => x.OriginalAmount)
            .ToList();

        Assert.Equal(2, settlements.Count);

        var thirty = settlements[0];
        var fifty = settlements[1];

        Assert.Equal(30m, thirty.OriginalAmount);
        Assert.Equal("settled", thirty.Status);
        Assert.Equal(0m, thirty.RemainingAmount);

        Assert.Equal(50m, fifty.OriginalAmount);
        Assert.Equal("open", fifty.Status);
        Assert.Equal(50m, fifty.RemainingAmount);
    }

    [Fact]
    public async Task Node_Search_Finds_Canonical_Entity_By_Role_And_Alias()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "le mie figlie sono da mia madre");
        await fixture.ProcessAsync(user.Id, "mia madre si chiama Felicia");
        await fixture.ProcessAsync(user.Id, "oggi Felia era con le bambine");

        await using var db = fixture.CreateDbContext();
        var service = new CognitiveGraphService(
            db,
            new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
            new NoOpEntityRetrievalService(),
            new FeedbackPolicyService(db),
            new ClarificationService(db),
            new NullLogger<CognitiveGraphService>());

        var byRole = await service.SearchNodesAsync(user.Id, "madre", 10);
        var byAlias = await service.SearchNodesAsync(user.Id, "felia", 10);

        Assert.NotEmpty(byRole.Items);
        Assert.NotEmpty(byAlias.Items);
        Assert.Equal(byRole.Items[0].Id, byAlias.Items[0].Id);
    }

    [Fact]
    public async Task Search_Suppresses_Person_When_Same_Name_Place_Exists()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "ho sentito Bressana oggi");
        await fixture.ProcessAsync(user.Id, "oggi sono a Bressana");

        await using var db = fixture.CreateDbContext();
        var service = new CognitiveGraphService(
            db,
            new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
            new NoOpEntityRetrievalService(),
            new FeedbackPolicyService(db),
            new ClarificationService(db),
            new NullLogger<CognitiveGraphService>());

        var allNodes = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "bressana")
            .Select(x => x.Kind)
            .ToListAsync();

        Assert.Contains("place", allNodes);
        Assert.DoesNotContain("person", allNodes);

        var visible = await service.SearchNodesAsync(user.Id, "bressana", 20);
        Assert.Contains(visible.Items, x => x.Kind == "place" && x.CanonicalName == "Bressana");
        Assert.DoesNotContain(visible.Items, x => x.Kind == "person" && x.CanonicalName == "Bressana");
    }

    [Fact]
    public async Task Creates_Varied_Node_Types_From_Concepts_And_Places()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "Milano", Type = "place" },
                new ExtractedConcept { Label = "Atlas", Type = "project" },
                new ExtractedConcept { Label = "Stoicismo", Type = "philosophy" }
            ]
        };

        await fixture.ProcessAsync(user.Id, "oggi sono in Milano, lavoro al progetto Atlas e penso allo stoicismo", analysis);

        await using var db = fixture.CreateDbContext();
        var nodes = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id)
            .Select(x => new { x.CanonicalName, x.Kind })
            .ToListAsync();

        Assert.Contains(nodes, x => x.CanonicalName == "Milano" && x.Kind == "place");
        Assert.Contains(nodes, x => x.CanonicalName == "Atlas" && x.Kind == "project");
        Assert.Contains(nodes, x => x.CanonicalName == "Stoicismo" && x.Kind == "idea");
    }

    [Fact]
    public async Task Uses_Primary_Topic_Only_When_Entry_Contains_Multiple_Sentences()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "Atlas", Type = "project" },
                new ExtractedConcept { Label = "Palestra", Type = "activity" }
            ]
        };

        await fixture.ProcessAsync(
            user.Id,
            "Sto lavorando al progetto Atlas con focus totale. Poi sono andato in palestra e ho mangiato pizza.",
            analysis);

        await using var db = fixture.CreateDbContext();
        var nodes = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id)
            .Select(x => new { x.NormalizedCanonicalName, x.Kind })
            .ToListAsync();

        Assert.Contains(nodes, x => x.NormalizedCanonicalName == "atlas" && x.Kind == "project");
        Assert.DoesNotContain(nodes, x => x.NormalizedCanonicalName == "palestra");
    }

    [Fact]
    public async Task Does_Not_Create_Generic_Detail_Nodes_For_Food_And_Time()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "Pizza", Type = "food" },
                new ExtractedConcept { Label = "Pasta", Type = "object" },
                new ExtractedConcept { Label = "Ore", Type = "generic" }
            ]
        };

        await fixture.ProcessAsync(user.Id, "Oggi ho mangiato pizza e pasta per ore.", analysis);

        await using var db = fixture.CreateDbContext();
        var nodes = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id)
            .Select(x => x.NormalizedCanonicalName)
            .ToListAsync();

        Assert.DoesNotContain("pizza", nodes);
        Assert.DoesNotContain("pasta", nodes);
        Assert.DoesNotContain("ore", nodes);
    }

    [Fact]
    public async Task Does_Not_Classify_Standalone_Place_As_Person()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "oggi sono in Milano tutto il giorno");

        await using var db = fixture.CreateDbContext();
        var milanoNodes = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "milano")
            .Select(x => new { x.CanonicalName, x.Kind })
            .ToListAsync();

        var node = Assert.Single(milanoNodes);
        Assert.Equal("place", node.Kind);
    }

    [Fact]
    public async Task Avoids_Person_False_Positive_For_Stopword_And_Sports_Team()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "alle", Type = "person" },
                new ExtractedConcept { Label = "Milan", Type = "person" },
                new ExtractedConcept { Label = "Milan", Type = "team" }
            ]
        };

        await fixture.ProcessAsync(user.Id, "alle 12:30 oggi giochera il Milan.", analysis);

        await using var db = fixture.CreateDbContext();
        var alleKinds = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "alle")
            .Select(x => x.Kind)
            .ToListAsync();

        var milanKinds = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "milan")
            .Select(x => x.Kind)
            .ToListAsync();

        Assert.DoesNotContain("person", alleKinds);
        Assert.DoesNotContain("person", milanKinds);
        Assert.Contains("team", milanKinds);
    }

    [Fact]
    public async Task Does_Not_Create_Financial_Event_From_NonMoney_Devo_Sentence()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await fixture.ProcessAsync(user.Id, "Devo dare piu attenzioni a Irina, mia moglie, che dice che parlo poco con lei.");

        await using var db = fixture.CreateDbContext();
        var eventCount = await db.MemoryEvents.CountAsync(x => x.UserId == user.Id);
        Assert.Equal(0, eventCount);

        var leiNodes = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "lei")
            .Select(x => x.Kind)
            .ToListAsync();

        Assert.Empty(leiNodes);
    }

    [Fact]
    public async Task Does_Not_Create_Pronoun_Person_From_Ai_Concept()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "lei", Type = "person" }
            ]
        };

        await fixture.ProcessAsync(user.Id, "Devo parlare con lei oggi.", analysis);

        await using var db = fixture.CreateDbContext();
        var leiKinds = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "lei")
            .Select(x => x.Kind)
            .ToListAsync();

        Assert.Empty(leiKinds);
    }

    [Fact]
    public async Task Does_Not_Create_Place_For_Strong_Person_Name_After_Preposition()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "Irina", Type = "person" }
            ]
        };

        await fixture.ProcessAsync(user.Id, "Devo parlare a Irina oggi.", analysis);

        await using var db = fixture.CreateDbContext();
        var irinaKinds = await db.CanonicalEntities
            .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "irina")
            .Select(x => x.Kind)
            .ToListAsync();

        Assert.Contains("person", irinaKinds);
        Assert.DoesNotContain("place", irinaKinds);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

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

        public async Task<User> CreateUserAsync()
        {
            await using var db = CreateDbContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = "test"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public async Task ProcessAsync(Guid userId, string content, AiAnalysisResult? analysis = null)
        {
            await using var db = CreateDbContext();
            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            db.Entries.Add(entry);
            await db.SaveChangesAsync();

            var service = new CognitiveGraphService(
                db,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                new NoOpEntityRetrievalService(),
                new FeedbackPolicyService(db),
                new ClarificationService(db),
                new NullLogger<CognitiveGraphService>());

            await service.ProcessEntryAsync(entry, analysis ?? new AiAnalysisResult());
        }

        public AppDbContext CreateDbContext() => new(_options);

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
