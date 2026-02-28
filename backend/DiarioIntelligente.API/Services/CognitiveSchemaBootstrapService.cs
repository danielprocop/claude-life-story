using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public sealed class CognitiveSchemaBootstrapService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CognitiveSchemaBootstrapService> _logger;

    public CognitiveSchemaBootstrapService(
        IServiceProvider serviceProvider,
        ILogger<CognitiveSchemaBootstrapService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsNpgsql())
            await ExecuteStatementsAsync(db, PostgresStatements, cancellationToken);
        else if (db.Database.IsSqlite())
            await ExecuteStatementsAsync(db, SqliteStatements, cancellationToken);

        _logger.LogInformation("Cognitive graph schema bootstrap completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task ExecuteStatementsAsync(AppDbContext db, IEnumerable<string> statements, CancellationToken cancellationToken)
    {
        foreach (var statement in statements)
            await db.Database.ExecuteSqlRawAsync(statement, cancellationToken);
    }

    private static readonly string[] PostgresStatements =
    {
        """
        CREATE TABLE IF NOT EXISTS "CanonicalEntities" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
            "Kind" character varying(50) NOT NULL,
            "CanonicalName" character varying(256) NOT NULL,
            "NormalizedCanonicalName" character varying(256) NOT NULL,
            "AnchorKey" character varying(100) NULL,
            "EntityCard" text NOT NULL,
            "Description" text NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        )
        """,
        """CREATE INDEX IF NOT EXISTS "IX_CanonicalEntities_UserId_Kind" ON "CanonicalEntities" ("UserId", "Kind")""",
        """CREATE INDEX IF NOT EXISTS "IX_CanonicalEntities_UserId_NormalizedCanonicalName" ON "CanonicalEntities" ("UserId", "NormalizedCanonicalName")""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CanonicalEntities_UserId_AnchorKey" ON "CanonicalEntities" ("UserId", "AnchorKey")""",
        """
        CREATE TABLE IF NOT EXISTS "EntityAliases" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "EntityId" uuid NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "Alias" character varying(256) NOT NULL,
            "NormalizedAlias" character varying(256) NOT NULL,
            "AliasType" character varying(50) NOT NULL,
            "Confidence" real NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_EntityAliases_EntityId_NormalizedAlias" ON "EntityAliases" ("EntityId", "NormalizedAlias")""",
        """
        CREATE TABLE IF NOT EXISTS "EntityEvidence" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "EntityId" uuid NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "EntryId" uuid NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "EvidenceType" character varying(50) NOT NULL,
            "Snippet" text NOT NULL,
            "PropertyName" character varying(100) NULL,
            "Value" text NULL,
            "MergeReason" text NULL,
            "Confidence" real NOT NULL,
            "RecordedAt" timestamp with time zone NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_EntityEvidence_EntityId_EntryId_EvidenceType_Snippet" ON "EntityEvidence" ("EntityId", "EntryId", "EvidenceType", "Snippet")""",
        """
        CREATE TABLE IF NOT EXISTS "MemoryEvents" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
            "EntityId" uuid NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "SourceEntryId" uuid NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "EventType" character varying(50) NOT NULL,
            "Title" character varying(300) NOT NULL,
            "OccurredAt" timestamp with time zone NOT NULL,
            "IncludesUser" boolean NOT NULL,
            "Currency" character varying(10) NOT NULL,
            "EventTotal" numeric NULL,
            "MyShare" numeric NULL,
            "Notes" text NULL,
            "SourceSnippet" text NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_MemoryEvents_UserId_SourceEntryId" ON "MemoryEvents" ("UserId", "SourceEntryId")""",
        """CREATE INDEX IF NOT EXISTS "IX_MemoryEvents_UserId_OccurredAt" ON "MemoryEvents" ("UserId", "OccurredAt")""",
        """
        CREATE TABLE IF NOT EXISTS "EventParticipants" (
            "EventId" uuid NOT NULL REFERENCES "MemoryEvents"("Id") ON DELETE CASCADE,
            "EntityId" uuid NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "Role" character varying(30) NOT NULL,
            PRIMARY KEY ("EventId", "EntityId")
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS "Settlements" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
            "EventId" uuid NULL REFERENCES "MemoryEvents"("Id") ON DELETE SET NULL,
            "CounterpartyEntityId" uuid NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "SourceEntryId" uuid NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "Direction" character varying(30) NOT NULL,
            "OriginalAmount" numeric NOT NULL,
            "RemainingAmount" numeric NOT NULL,
            "Currency" character varying(10) NOT NULL,
            "Status" character varying(30) NOT NULL,
            "Notes" text NULL,
            "SourceSnippet" text NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        )
        """,
        """CREATE INDEX IF NOT EXISTS "IX_Settlements_UserId_CounterpartyEntityId_Status" ON "Settlements" ("UserId", "CounterpartyEntityId", "Status")""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Settlements_UserId_SourceEntryId_CounterpartyEntityId_Direction_OriginalAmount" ON "Settlements" ("UserId", "SourceEntryId", "CounterpartyEntityId", "Direction", "OriginalAmount")""",
        """
        CREATE TABLE IF NOT EXISTS "SettlementPayments" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "SettlementId" uuid NOT NULL REFERENCES "Settlements"("Id") ON DELETE CASCADE,
            "EntryId" uuid NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "Amount" numeric NOT NULL,
            "Snippet" text NULL,
            "RecordedAt" timestamp with time zone NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SettlementPayments_SettlementId_EntryId_Amount" ON "SettlementPayments" ("SettlementId", "EntryId", "Amount")"""
    };

    private static readonly string[] SqliteStatements =
    {
        """
        CREATE TABLE IF NOT EXISTS "CanonicalEntities" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "UserId" TEXT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
            "Kind" TEXT NOT NULL,
            "CanonicalName" TEXT NOT NULL,
            "NormalizedCanonicalName" TEXT NOT NULL,
            "AnchorKey" TEXT NULL,
            "EntityCard" TEXT NOT NULL,
            "Description" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        )
        """,
        """CREATE INDEX IF NOT EXISTS "IX_CanonicalEntities_UserId_Kind" ON "CanonicalEntities" ("UserId", "Kind")""",
        """CREATE INDEX IF NOT EXISTS "IX_CanonicalEntities_UserId_NormalizedCanonicalName" ON "CanonicalEntities" ("UserId", "NormalizedCanonicalName")""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CanonicalEntities_UserId_AnchorKey" ON "CanonicalEntities" ("UserId", "AnchorKey")""",
        """
        CREATE TABLE IF NOT EXISTS "EntityAliases" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "EntityId" TEXT NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "Alias" TEXT NOT NULL,
            "NormalizedAlias" TEXT NOT NULL,
            "AliasType" TEXT NOT NULL,
            "Confidence" REAL NOT NULL,
            "CreatedAt" TEXT NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_EntityAliases_EntityId_NormalizedAlias" ON "EntityAliases" ("EntityId", "NormalizedAlias")""",
        """
        CREATE TABLE IF NOT EXISTS "EntityEvidence" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "EntityId" TEXT NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "EntryId" TEXT NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "EvidenceType" TEXT NOT NULL,
            "Snippet" TEXT NOT NULL,
            "PropertyName" TEXT NULL,
            "Value" TEXT NULL,
            "MergeReason" TEXT NULL,
            "Confidence" REAL NOT NULL,
            "RecordedAt" TEXT NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_EntityEvidence_EntityId_EntryId_EvidenceType_Snippet" ON "EntityEvidence" ("EntityId", "EntryId", "EvidenceType", "Snippet")""",
        """
        CREATE TABLE IF NOT EXISTS "MemoryEvents" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "UserId" TEXT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
            "EntityId" TEXT NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "SourceEntryId" TEXT NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "EventType" TEXT NOT NULL,
            "Title" TEXT NOT NULL,
            "OccurredAt" TEXT NOT NULL,
            "IncludesUser" INTEGER NOT NULL,
            "Currency" TEXT NOT NULL,
            "EventTotal" NUMERIC NULL,
            "MyShare" NUMERIC NULL,
            "Notes" TEXT NULL,
            "SourceSnippet" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_MemoryEvents_UserId_SourceEntryId" ON "MemoryEvents" ("UserId", "SourceEntryId")""",
        """CREATE INDEX IF NOT EXISTS "IX_MemoryEvents_UserId_OccurredAt" ON "MemoryEvents" ("UserId", "OccurredAt")""",
        """
        CREATE TABLE IF NOT EXISTS "EventParticipants" (
            "EventId" TEXT NOT NULL REFERENCES "MemoryEvents"("Id") ON DELETE CASCADE,
            "EntityId" TEXT NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "Role" TEXT NOT NULL,
            PRIMARY KEY ("EventId", "EntityId")
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS "Settlements" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "UserId" TEXT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
            "EventId" TEXT NULL REFERENCES "MemoryEvents"("Id") ON DELETE SET NULL,
            "CounterpartyEntityId" TEXT NOT NULL REFERENCES "CanonicalEntities"("Id") ON DELETE CASCADE,
            "SourceEntryId" TEXT NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "Direction" TEXT NOT NULL,
            "OriginalAmount" NUMERIC NOT NULL,
            "RemainingAmount" NUMERIC NOT NULL,
            "Currency" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "Notes" TEXT NULL,
            "SourceSnippet" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        )
        """,
        """CREATE INDEX IF NOT EXISTS "IX_Settlements_UserId_CounterpartyEntityId_Status" ON "Settlements" ("UserId", "CounterpartyEntityId", "Status")""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Settlements_UserId_SourceEntryId_CounterpartyEntityId_Direction_OriginalAmount" ON "Settlements" ("UserId", "SourceEntryId", "CounterpartyEntityId", "Direction", "OriginalAmount")""",
        """
        CREATE TABLE IF NOT EXISTS "SettlementPayments" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "SettlementId" TEXT NOT NULL REFERENCES "Settlements"("Id") ON DELETE CASCADE,
            "EntryId" TEXT NOT NULL REFERENCES "Entries"("Id") ON DELETE CASCADE,
            "Amount" NUMERIC NOT NULL,
            "Snippet" TEXT NULL,
            "RecordedAt" TEXT NOT NULL
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SettlementPayments_SettlementId_EntryId_Amount" ON "SettlementPayments" ("SettlementId", "EntryId", "Amount")"""
    };
}
