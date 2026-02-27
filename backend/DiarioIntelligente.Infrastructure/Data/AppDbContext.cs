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
    }
}
