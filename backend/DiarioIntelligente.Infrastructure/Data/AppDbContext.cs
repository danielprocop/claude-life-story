using DiarioIntelligente.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<EntryConceptMap> EntryConceptMaps => Set<EntryConceptMap>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<EnergyLog> EnergyLogs => Set<EnergyLog>();
    public DbSet<GoalItem> GoalItems => Set<GoalItem>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CanonicalEntity> CanonicalEntities => Set<CanonicalEntity>();
    public DbSet<EntityAlias> EntityAliases => Set<EntityAlias>();
    public DbSet<EntityEvidence> EntityEvidence => Set<EntityEvidence>();
    public DbSet<MemoryEvent> MemoryEvents => Set<MemoryEvent>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementPayment> SettlementPayments => Set<SettlementPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        // Entry
        modelBuilder.Entity<Entry>(e =>
        {
            e.HasKey(en => en.Id);
            e.Property(en => en.Content).IsRequired();
            e.HasOne(en => en.User)
                .WithMany(u => u.Entries)
                .HasForeignKey(en => en.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(en => new { en.UserId, en.CreatedAt });
        });

        // Concept
        modelBuilder.Entity<Concept>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Label).HasMaxLength(256).IsRequired();
            e.Property(c => c.Type).HasMaxLength(50).IsRequired();
            e.HasOne(c => c.User)
                .WithMany(u => u.Concepts)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.UserId, c.Label, c.Type }).IsUnique();
        });

        // EntryConceptMap (join table)
        modelBuilder.Entity<EntryConceptMap>(e =>
        {
            e.HasKey(m => new { m.EntryId, m.ConceptId });
            e.HasOne(m => m.Entry)
                .WithMany(en => en.EntryConceptMaps)
                .HasForeignKey(m => m.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Concept)
                .WithMany(c => c.EntryConceptMaps)
                .HasForeignKey(m => m.ConceptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Connection (composite key)
        modelBuilder.Entity<Connection>(e =>
        {
            e.HasKey(c => new { c.ConceptAId, c.ConceptBId });
            e.Property(c => c.Type).HasMaxLength(50);
            e.HasOne(c => c.ConceptA)
                .WithMany(co => co.ConnectionsAsA)
                .HasForeignKey(c => c.ConceptAId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.ConceptB)
                .WithMany(co => co.ConnectionsAsB)
                .HasForeignKey(c => c.ConceptBId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Insight
        modelBuilder.Entity<Insight>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Content).IsRequired();
            e.Property(i => i.Type).HasMaxLength(50).IsRequired();
            e.HasOne(i => i.User)
                .WithMany(u => u.Insights)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(i => new { i.UserId, i.GeneratedAt });
        });

        // EnergyLog
        modelBuilder.Entity<EnergyLog>(e =>
        {
            e.HasKey(el => el.Id);
            e.HasOne(el => el.Entry)
                .WithMany()
                .HasForeignKey(el => el.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(el => el.User)
                .WithMany(u => u.EnergyLogs)
                .HasForeignKey(el => el.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(el => new { el.UserId, el.RecordedAt });
        });

        // GoalItem (self-referencing hierarchy)
        modelBuilder.Entity<GoalItem>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Title).HasMaxLength(500).IsRequired();
            e.Property(g => g.Status).HasMaxLength(20).IsRequired();
            e.HasOne(g => g.User)
                .WithMany(u => u.GoalItems)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(g => g.ParentGoal)
                .WithMany(g => g.SubGoals)
                .HasForeignKey(g => g.ParentGoalId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(g => new { g.UserId, g.Status });
        });

        // ChatMessage
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Role).HasMaxLength(20).IsRequired();
            e.Property(m => m.Content).IsRequired();
            e.HasOne(m => m.User)
                .WithMany(u => u.ChatMessages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.UserId, m.CreatedAt });
        });

        // CanonicalEntity
        modelBuilder.Entity<CanonicalEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(50).IsRequired();
            e.Property(x => x.CanonicalName).HasMaxLength(256).IsRequired();
            e.Property(x => x.NormalizedCanonicalName).HasMaxLength(256).IsRequired();
            e.Property(x => x.AnchorKey).HasMaxLength(100);
            e.Property(x => x.EntityCard).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(u => u.CanonicalEntities)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Kind });
            e.HasIndex(x => new { x.UserId, x.NormalizedCanonicalName });
            e.HasIndex(x => new { x.UserId, x.AnchorKey }).IsUnique();
        });

        // EntityAlias
        modelBuilder.Entity<EntityAlias>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Alias).HasMaxLength(256).IsRequired();
            e.Property(x => x.NormalizedAlias).HasMaxLength(256).IsRequired();
            e.Property(x => x.AliasType).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.Entity)
                .WithMany(x => x.Aliases)
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EntityId, x.NormalizedAlias }).IsUnique();
        });

        // EntityEvidence
        modelBuilder.Entity<EntityEvidence>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EvidenceType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Snippet).IsRequired();
            e.Property(x => x.PropertyName).HasMaxLength(100);
            e.HasOne(x => x.Entity)
                .WithMany(x => x.Evidence)
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entry)
                .WithMany()
                .HasForeignKey(x => x.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EntityId, x.EntryId, x.EvidenceType, x.Snippet }).IsUnique();
        });

        // MemoryEvent
        modelBuilder.Entity<MemoryEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(u => u.MemoryEvents)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entity)
                .WithMany()
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SourceEntry)
                .WithMany()
                .HasForeignKey(x => x.SourceEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.OccurredAt });
            e.HasIndex(x => new { x.UserId, x.SourceEntryId }).IsUnique();
        });

        // EventParticipant
        modelBuilder.Entity<EventParticipant>(e =>
        {
            e.HasKey(x => new { x.EventId, x.EntityId });
            e.Property(x => x.Role).HasMaxLength(30).IsRequired();
            e.HasOne(x => x.Event)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entity)
                .WithMany(x => x.EventParticipants)
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Settlement
        modelBuilder.Entity<Settlement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Direction).HasMaxLength(30).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(u => u.Settlements)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Event)
                .WithMany(x => x.Settlements)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CounterpartyEntity)
                .WithMany(x => x.Settlements)
                .HasForeignKey(x => x.CounterpartyEntityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SourceEntry)
                .WithMany()
                .HasForeignKey(x => x.SourceEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.CounterpartyEntityId, x.Status });
            e.HasIndex(x => new { x.UserId, x.SourceEntryId, x.CounterpartyEntityId, x.Direction, x.OriginalAmount }).IsUnique();
        });

        // SettlementPayment
        modelBuilder.Entity<SettlementPayment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Settlement)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entry)
                .WithMany()
                .HasForeignKey(x => x.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SettlementId, x.EntryId, x.Amount }).IsUnique();
        });
    }
}
