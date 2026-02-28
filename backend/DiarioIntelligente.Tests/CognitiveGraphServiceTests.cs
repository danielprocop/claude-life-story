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

        public async Task ProcessAsync(Guid userId, string content)
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
                new NullLogger<CognitiveGraphService>());

            await service.ProcessEntryAsync(entry, new AiAnalysisResult());
        }

        public AppDbContext CreateDbContext() => new(_options);

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
