using System.Globalization;
using System.Text;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiarioIntelligente.Tests;

public class EntityNormalizationServiceTests
{
    [Fact]
    public async Task Normalize_Merges_Weak_Person_Into_Place_And_Is_Idempotent()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await using (var db = fixture.CreateDbContext())
        {
            var place = CreateEntity(user.Id, "place", "Bressana");
            var person = CreateEntity(user.Id, "person", "Bressana");
            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Content = "Bressana centro",
                CreatedAt = DateTime.UtcNow
            };

            person.Aliases.Add(new EntityAlias
            {
                Id = Guid.NewGuid(),
                EntityId = person.Id,
                Entity = person,
                Alias = "Bressana Centro",
                NormalizedAlias = Normalize("Bressana Centro"),
                AliasType = "observed_name",
                Confidence = 0.9f
            });
            person.Evidence.Add(new EntityEvidence
            {
                Id = Guid.NewGuid(),
                EntityId = person.Id,
                Entity = person,
                EntryId = entry.Id,
                Entry = entry,
                EvidenceType = "mention",
                Snippet = "Bressana",
                Confidence = 0.8f
            });

            db.Entries.Add(entry);
            db.CanonicalEntities.Add(place);
            db.CanonicalEntities.Add(person);
            await db.SaveChangesAsync();
        }

        NormalizeEntitiesResponse first;
        await using (var db = fixture.CreateDbContext())
        {
            var service = new EntityNormalizationService(
                db,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                new NullLogger<EntityNormalizationService>());

            first = await service.NormalizeUserEntitiesAsync(user.Id);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var nodes = await db.CanonicalEntities
                .Where(x => x.UserId == user.Id && x.NormalizedCanonicalName == "bressana" && x.Kind == "place")
                .Include(x => x.Aliases)
                .ToListAsync();

            var place = Assert.Single(nodes);
            Assert.Equal("place", place.Kind);
            Assert.Contains(place.Aliases, x => x.NormalizedAlias == "bressanacentro");
        }

        Assert.Equal(1, first.Normalized);
        Assert.Equal(1, first.Merged);
        Assert.Equal(1, first.Suppressed);
        Assert.Equal(0, first.Ambiguous);

        await using (var db = fixture.CreateDbContext())
        {
            var service = new EntityNormalizationService(
                db,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                new NullLogger<EntityNormalizationService>());

            var second = await service.NormalizeUserEntitiesAsync(user.Id);
            Assert.Equal(0, second.Normalized);
            Assert.Equal(0, second.Merged);
            Assert.Equal(0, second.Suppressed);
            Assert.Equal(0, second.Ambiguous);
        }
    }

    [Fact]
    public async Task Normalize_Does_Not_Merge_Strong_Person_Conflict()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        Guid personId;
        await using (var db = fixture.CreateDbContext())
        {
            var place = CreateEntity(user.Id, "place", "Bressana");
            var person = CreateEntity(user.Id, "person", "Bressana");
            personId = person.Id;

            var sourceEntry = new Entry
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Content = "devo 20 a Bressana",
                CreatedAt = DateTime.UtcNow
            };

            db.CanonicalEntities.Add(place);
            db.CanonicalEntities.Add(person);
            db.Entries.Add(sourceEntry);
            db.Settlements.Add(new Settlement
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CounterpartyEntityId = person.Id,
                SourceEntryId = sourceEntry.Id,
                Direction = "user_owes",
                OriginalAmount = 20m,
                RemainingAmount = 20m,
                Currency = "EUR",
                Status = "open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var service = new EntityNormalizationService(
                db,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                new NullLogger<EntityNormalizationService>());

            var response = await service.NormalizeUserEntitiesAsync(user.Id);
            Assert.Equal(1, response.Normalized);
            Assert.Equal(0, response.Merged);
            Assert.Equal(0, response.Suppressed);
            Assert.Equal(1, response.Ambiguous);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var count = await db.CanonicalEntities.CountAsync(x =>
                x.UserId == user.Id &&
                x.NormalizedCanonicalName == "bressana" &&
                (x.Kind == "place" || x.Kind == "person"));
            Assert.Equal(2, count);

            var personStillThere = await db.CanonicalEntities.AnyAsync(x => x.Id == personId && x.Kind == "person");
            Assert.True(personStillThere);
        }
    }

    [Fact]
    public async Task Normalize_Never_Drops_Anchor_Person()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();

        await using (var db = fixture.CreateDbContext())
        {
            db.CanonicalEntities.Add(CreateEntity(user.Id, "place", "Madre"));
            db.CanonicalEntities.Add(CreateEntity(user.Id, "person", "Madre", "mother_of_user"));
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var service = new EntityNormalizationService(
                db,
                new NoOpSearchProjectionService(new NullLogger<NoOpSearchProjectionService>()),
                new NullLogger<EntityNormalizationService>());

            var response = await service.NormalizeUserEntitiesAsync(user.Id);
            Assert.Equal(0, response.Merged);
            Assert.Equal(0, response.Suppressed);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var anchorExists = await db.CanonicalEntities.AnyAsync(x =>
                x.UserId == user.Id &&
                x.AnchorKey == "mother_of_user" &&
                x.Kind == "person");
            Assert.True(anchorExists);
        }
    }

    private static CanonicalEntity CreateEntity(Guid userId, string kind, string name, string? anchorKey = null)
    {
        return new CanonicalEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            CanonicalName = name,
            NormalizedCanonicalName = Normalize(name),
            AnchorKey = anchorKey,
            EntityCard = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string Normalize(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
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

        public AppDbContext CreateDbContext() => new(_options);

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

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
