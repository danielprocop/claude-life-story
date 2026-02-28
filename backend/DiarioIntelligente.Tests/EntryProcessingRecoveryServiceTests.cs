using DiarioIntelligente.API.Services;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiarioIntelligente.Tests;

public class EntryProcessingRecoveryServiceTests
{
    [Fact]
    public async Task Queues_Unprocessed_New_Entry_On_Startup()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var entry = await fixture.CreateEntryAsync(user.Id, "nota senza processing state", updatedAt: null);
        await fixture.CreateCanonicalEntityAsync(user.Id);

        var service = fixture.CreateRecoveryService();
        await service.StartAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var job = await fixture.ProcessingQueue.DequeueAsync(cts.Token);
        Assert.Equal(entry.Id, job.EntryId);
        Assert.Equal(user.Id, job.UserId);
    }

    [Fact]
    public async Task Queues_User_Rebuild_When_Updated_Entry_State_Is_Stale()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var updatedAt = DateTime.UtcNow;
        var entry = await fixture.CreateEntryAsync(user.Id, "entry aggiornata", updatedAt);
        await fixture.CreateCanonicalEntityAsync(user.Id);
        await fixture.CreateProcessingStateAsync(entry.Id, user.Id, updatedAt.AddMinutes(-5), usedAiAnalysis: false);

        var service = fixture.CreateRecoveryService();
        await service.StartAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var rebuildUserId = await fixture.RebuildQueue.DequeueAsync(cts.Token);
        Assert.Equal(user.Id, rebuildUserId);
    }

    [Fact]
    public async Task Does_Not_Queue_Already_Processed_New_Entry()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var entry = await fixture.CreateEntryAsync(user.Id, "entry gia processata", updatedAt: null);
        await fixture.CreateCanonicalEntityAsync(user.Id);
        await fixture.CreateProcessingStateAsync(entry.Id, user.Id, sourceUpdatedAt: null, usedAiAnalysis: false);

        var service = fixture.CreateRecoveryService();
        await service.StartAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.ProcessingQueue.DequeueAsync(cts.Token));
    }

    private sealed class RecoveryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly ServiceProvider _provider;

        private RecoveryFixture(
            SqliteConnection connection,
            DbContextOptions<AppDbContext> options,
            ServiceProvider provider,
            EntryProcessingQueue processingQueue,
            UserMemoryRebuildQueue rebuildQueue)
        {
            _connection = connection;
            _options = options;
            _provider = provider;
            ProcessingQueue = processingQueue;
            RebuildQueue = rebuildQueue;
        }

        public EntryProcessingQueue ProcessingQueue { get; }
        public UserMemoryRebuildQueue RebuildQueue { get; }

        public static async Task<RecoveryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(builder => builder.UseSqlite(connection));
            services.AddSingleton<EntryProcessingQueue>();
            services.AddSingleton<UserMemoryRebuildQueue>();
            var provider = services.BuildServiceProvider();

            return new RecoveryFixture(
                connection,
                options,
                provider,
                provider.GetRequiredService<EntryProcessingQueue>(),
                provider.GetRequiredService<UserMemoryRebuildQueue>());
        }

        public EntryProcessingRecoveryService CreateRecoveryService()
        {
            return new EntryProcessingRecoveryService(
                _provider,
                NullLogger<EntryProcessingRecoveryService>.Instance);
        }

        public async Task<User> CreateUserAsync()
        {
            await using var db = new AppDbContext(_options);
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"{Guid.NewGuid():N}@recovery.test",
                PasswordHash = "test"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public async Task<Entry> CreateEntryAsync(Guid userId, string content, DateTime? updatedAt)
        {
            await using var db = new AppDbContext(_options);
            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = updatedAt
            };
            db.Entries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        }

        public async Task CreateCanonicalEntityAsync(Guid userId)
        {
            await using var db = new AppDbContext(_options);
            db.CanonicalEntities.Add(new CanonicalEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = "person",
                CanonicalName = "Anchor",
                NormalizedCanonicalName = "anchor",
                AnchorKey = "mother_of_user",
                EntityCard = "Anchor",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public async Task CreateProcessingStateAsync(Guid entryId, Guid userId, DateTime? sourceUpdatedAt, bool usedAiAnalysis)
        {
            await using var db = new AppDbContext(_options);
            db.EntryProcessingStates.Add(new EntryProcessingState
            {
                EntryId = entryId,
                UserId = userId,
                SourceUpdatedAt = sourceUpdatedAt,
                LastProcessedAt = DateTime.UtcNow,
                UsedAiAnalysis = usedAiAnalysis
            });
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
