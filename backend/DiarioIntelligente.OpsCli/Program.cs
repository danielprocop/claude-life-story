using System.Text.Json;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class Program
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonlJson = new()
    {
        WriteIndented = false
    };

    private static readonly HashSet<string> PronounLikeTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "io", "me", "mio", "mia", "miei", "mie",
        "tu", "te", "tuo", "tua", "tuoi", "tue",
        "lui", "lei", "gli", "le",
        "noi", "voi", "loro",
        "mi", "ti", "ci", "vi", "si",
        "questa", "questo", "quello", "quella"
    };

    private static readonly HashSet<string> StopwordLikeTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "inoltre", "poi", "anche", "quindi", "pero", "però", "comunque",
        "oggi", "ieri", "domani", "adesso", "dopo", "prima",
        "qui", "qua", "li", "lì",
        "devo", "bisogna", "fare", "fatto"
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var command = args.FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(command) || command is "-h" or "--help" or "help")
            {
                PrintHelp();
                return 0;
            }

            var options = ParseOptions(args.Skip(1).ToArray());
            var connectionString = options.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("Missing connection string. Provide --connection or set DIARIO_CONNECTION_STRING.");
                return 2;
            }

            var outDir = options.OutDir;
            if (string.IsNullOrWhiteSpace(outDir))
            {
                var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                outDir = Path.Combine(Directory.GetCurrentDirectory(), ".runlogs", "data-quality", runId);
            }

            Directory.CreateDirectory(outDir);

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using var db = new AppDbContext(dbOptions);

            return command switch
            {
                "audit" => await AuditAsync(db, outDir, options),
                "export" => await ExportAsync(db, outDir, options),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("DiarioIntelligente.OpsCli");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  audit   Export key tables + run data-quality checks");
        Console.WriteLine("  export  Export key tables (JSONL)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --connection <str>              DB connection string (or DIARIO_CONNECTION_STRING env var)");
        Console.WriteLine("  --out <dir>                     Output directory (default: .runlogs/data-quality/<timestamp>)");
        Console.WriteLine("  --user <guid>                   Restrict to one user");
        Console.WriteLine("  --include-entry-content         Include full entry content in export (sensitive)");
        Console.WriteLine("  --no-export                     Skip exporting JSONL files during audit");
    }

    private sealed record Options(
        string? ConnectionString,
        string? OutDir,
        Guid? UserId,
        bool IncludeEntryContent,
        bool ExportDuringAudit);

    private static Options ParseOptions(string[] args)
    {
        string? connectionString = null;
        string? outDir = null;
        Guid? userId = null;
        var includeEntryContent = false;
        var exportDuringAudit = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--connection", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                connectionString = args[++i];
                continue;
            }

            if (string.Equals(arg, "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outDir = args[++i];
                continue;
            }

            if (string.Equals(arg, "--user", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (Guid.TryParse(args[++i], out var parsed))
                    userId = parsed;
                continue;
            }

            if (string.Equals(arg, "--include-entry-content", StringComparison.OrdinalIgnoreCase))
            {
                includeEntryContent = true;
                continue;
            }

            if (string.Equals(arg, "--no-export", StringComparison.OrdinalIgnoreCase))
            {
                exportDuringAudit = false;
                continue;
            }
        }

        connectionString ??= Environment.GetEnvironmentVariable("DIARIO_CONNECTION_STRING");

        return new Options(connectionString, outDir, userId, includeEntryContent, exportDuringAudit);
    }

    private sealed record EntryRow(Guid Id, Guid UserId, DateTime CreatedAt, int ContentLength, string? Content);
    private sealed record CanonicalEntityRow(
        Guid Id,
        Guid UserId,
        string Kind,
        string CanonicalName,
        string NormalizedCanonicalName,
        string? AnchorKey,
        DateTime CreatedAt,
        DateTime UpdatedAt);
    private sealed record AliasRow(Guid Id, Guid EntityId, string Alias, string NormalizedAlias, string AliasType);
    private sealed record EvidenceRow(Guid Id, Guid EntityId, Guid EntryId, string EvidenceType, string Snippet, string? PropertyName, string? Value, float Confidence, DateTime RecordedAt);
    private sealed record MemoryEventRow(
        Guid Id,
        Guid UserId,
        Guid EntityId,
        Guid SourceEntryId,
        string EventType,
        string Title,
        DateTime OccurredAt,
        decimal? EventTotal,
        decimal? MyShare,
        string Currency);
    private sealed record ParticipantRow(Guid EventId, Guid EntityId, string Role);
    private sealed record SettlementRow(Guid Id, Guid UserId, Guid? EventId, Guid CounterpartyEntityId, Guid SourceEntryId, string Direction, decimal OriginalAmount, decimal RemainingAmount, string Currency, string Status);

    private static async Task<int> ExportAsync(AppDbContext db, string outDir, Options options)
    {
        var exportDir = Path.Combine(outDir, "export");
        Directory.CreateDirectory(exportDir);

        var entriesQuery = db.Entries.AsNoTracking();
        var entitiesQuery = db.CanonicalEntities.AsNoTracking();
        var aliasesQuery = db.EntityAliases.AsNoTracking();
        var evidenceQuery = db.EntityEvidence.AsNoTracking();
        var eventsQuery = db.MemoryEvents.AsNoTracking();
        var participantsQuery = db.EventParticipants.AsNoTracking();
        var settlementsQuery = db.Settlements.AsNoTracking();

        if (options.UserId.HasValue)
        {
            var userId = options.UserId.Value;
            entriesQuery = entriesQuery.Where(x => x.UserId == userId);
            entitiesQuery = entitiesQuery.Where(x => x.UserId == userId);
            eventsQuery = eventsQuery.Where(x => x.UserId == userId);
            settlementsQuery = settlementsQuery.Where(x => x.UserId == userId);
        }

        await WriteJsonlAsync(
            Path.Combine(exportDir, "entries.jsonl"),
            entriesQuery.Select(x => new EntryRow(
                x.Id,
                x.UserId,
                x.CreatedAt,
                x.Content.Length,
                options.IncludeEntryContent ? x.Content : null)));

        await WriteJsonlAsync(
            Path.Combine(exportDir, "canonical_entities.jsonl"),
            entitiesQuery.Select(x => new CanonicalEntityRow(
                x.Id,
                x.UserId,
                x.Kind,
                x.CanonicalName,
                x.NormalizedCanonicalName,
                x.AnchorKey,
                x.CreatedAt,
                x.UpdatedAt)));

        await WriteJsonlAsync(
            Path.Combine(exportDir, "entity_aliases.jsonl"),
            aliasesQuery.Select(x => new AliasRow(x.Id, x.EntityId, x.Alias, x.NormalizedAlias, x.AliasType)));

        await WriteJsonlAsync(
            Path.Combine(exportDir, "entity_evidence.jsonl"),
            evidenceQuery.Select(x => new EvidenceRow(
                x.Id,
                x.EntityId,
                x.EntryId,
                x.EvidenceType,
                x.Snippet,
                x.PropertyName,
                x.Value,
                x.Confidence,
                x.RecordedAt)));

        await WriteJsonlAsync(
            Path.Combine(exportDir, "memory_events.jsonl"),
            eventsQuery.Select(x => new MemoryEventRow(
                x.Id,
                x.UserId,
                x.EntityId,
                x.SourceEntryId,
                x.EventType,
                x.Title,
                x.OccurredAt,
                x.EventTotal,
                x.MyShare,
                x.Currency)));

        await WriteJsonlAsync(
            Path.Combine(exportDir, "event_participants.jsonl"),
            participantsQuery.Select(x => new ParticipantRow(x.EventId, x.EntityId, x.Role)));

        await WriteJsonlAsync(
            Path.Combine(exportDir, "settlements.jsonl"),
            settlementsQuery.Select(x => new SettlementRow(
                x.Id,
                x.UserId,
                x.EventId,
                x.CounterpartyEntityId,
                x.SourceEntryId,
                x.Direction,
                x.OriginalAmount,
                x.RemainingAmount,
                x.Currency,
                x.Status)));

        var summary = new
        {
            exported_at_utc = DateTime.UtcNow,
            out_dir = outDir,
            user_filter = options.UserId,
            include_entry_content = options.IncludeEntryContent
        };
        await File.WriteAllTextAsync(Path.Combine(exportDir, "export_summary.json"), JsonSerializer.Serialize(summary, IndentedJson));

        Console.WriteLine($"Export written to: {exportDir}");
        return 0;
    }

    private static async Task<int> AuditAsync(AppDbContext db, string outDir, Options options)
    {
        if (options.ExportDuringAudit)
            await ExportAsync(db, outDir, options);

        var entriesQuery = db.Entries.AsNoTracking();
        var entitiesQuery = db.CanonicalEntities.AsNoTracking();
        var aliasesQuery = db.EntityAliases.AsNoTracking();
        var evidenceQuery = db.EntityEvidence.AsNoTracking();
        var eventsQuery = db.MemoryEvents.AsNoTracking();
        var participantsQuery = db.EventParticipants.AsNoTracking();
        var settlementsQuery = db.Settlements.AsNoTracking();

        if (options.UserId.HasValue)
        {
            var userId = options.UserId.Value;
            entriesQuery = entriesQuery.Where(x => x.UserId == userId);
            entitiesQuery = entitiesQuery.Where(x => x.UserId == userId);
            eventsQuery = eventsQuery.Where(x => x.UserId == userId);
            settlementsQuery = settlementsQuery.Where(x => x.UserId == userId);
        }

        var entries = await entriesQuery
            .Select(x => new EntryRow(x.Id, x.UserId, x.CreatedAt, x.Content.Length, null))
            .ToListAsync();
        var entities = await entitiesQuery
            .Select(x => new CanonicalEntityRow(x.Id, x.UserId, x.Kind, x.CanonicalName, x.NormalizedCanonicalName, x.AnchorKey, x.CreatedAt, x.UpdatedAt))
            .ToListAsync();
        var aliases = await aliasesQuery
            .Select(x => new AliasRow(x.Id, x.EntityId, x.Alias, x.NormalizedAlias, x.AliasType))
            .ToListAsync();
        var evidence = await evidenceQuery
            .Select(x => new EvidenceRow(x.Id, x.EntityId, x.EntryId, x.EvidenceType, x.Snippet, x.PropertyName, x.Value, x.Confidence, x.RecordedAt))
            .ToListAsync();
        var memoryEvents = await eventsQuery
            .Select(x => new MemoryEventRow(x.Id, x.UserId, x.EntityId, x.SourceEntryId, x.EventType, x.Title, x.OccurredAt, x.EventTotal, x.MyShare, x.Currency))
            .ToListAsync();
        var participants = await participantsQuery
            .Select(x => new ParticipantRow(x.EventId, x.EntityId, x.Role))
            .ToListAsync();
        var settlements = await settlementsQuery
            .Select(x => new SettlementRow(x.Id, x.UserId, x.EventId, x.CounterpartyEntityId, x.SourceEntryId, x.Direction, x.OriginalAmount, x.RemainingAmount, x.Currency, x.Status))
            .ToListAsync();

        var evidenceCountByEntityId = evidence
            .GroupBy(x => x.EntityId)
            .ToDictionary(x => x.Key, x => x.Count());

        var aliasCountByEntityId = aliases
            .GroupBy(x => x.EntityId)
            .ToDictionary(x => x.Key, x => x.Count());

        int EvidenceCount(Guid entityId) => evidenceCountByEntityId.TryGetValue(entityId, out var c) ? c : 0;
        int AliasCount(Guid entityId) => aliasCountByEntityId.TryGetValue(entityId, out var c) ? c : 0;

        var entitiesByKind = entities
            .GroupBy(x => x.Kind)
            .Select(g => new { kind = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var crossKindCollisions = entities
            .GroupBy(x => (x.UserId, x.NormalizedCanonicalName))
            .Select(g => new
            {
                g.Key.UserId,
                normalized = g.Key.NormalizedCanonicalName,
                kinds = g.Select(x => x.Kind).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                entityIds = g.Select(x => x.Id).ToList(),
                total_evidence = g.Sum(x => EvidenceCount(x.Id)),
                anchors = g.Where(x => !string.IsNullOrWhiteSpace(x.AnchorKey)).Select(x => x.AnchorKey).Distinct().ToList()
            })
            .Where(x => x.kinds.Count > 1)
            .OrderByDescending(x => x.total_evidence)
            .ToList();

        var duplicateWithinKind = entities
            .GroupBy(x => (x.UserId, kind: x.Kind.ToLowerInvariant(), x.NormalizedCanonicalName))
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                g.Key.UserId,
                kind = g.Key.kind,
                normalized = g.Key.NormalizedCanonicalName,
                entityIds = g.Select(x => x.Id).ToList(),
                evidence = g.Sum(x => EvidenceCount(x.Id))
            })
            .OrderByDescending(x => x.evidence)
            .ToList();

        var pronounPersons = entities
            .Where(x => string.Equals(x.Kind, "person", StringComparison.OrdinalIgnoreCase))
            .Where(x => PronounLikeTokens.Contains(x.NormalizedCanonicalName) || StopwordLikeTokens.Contains(x.NormalizedCanonicalName))
            .OrderByDescending(x => EvidenceCount(x.Id))
            .ThenBy(x => x.CanonicalName)
            .Select(x => new
            {
                x.UserId,
                x.Id,
                x.CanonicalName,
                x.NormalizedCanonicalName,
                evidence = EvidenceCount(x.Id),
                aliases = AliasCount(x.Id),
                anchor = x.AnchorKey
            })
            .ToList();

        var eventLikeEntities = entities
            .Where(x => string.Equals(x.Kind, "event", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.CanonicalName.StartsWith("Evento ", StringComparison.OrdinalIgnoreCase) || x.NormalizedCanonicalName.StartsWith("evento ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => EvidenceCount(x.Id))
            .Select(x => new
            {
                x.UserId,
                x.Id,
                x.CanonicalName,
                evidence = EvidenceCount(x.Id)
            })
            .ToList();

        var eventsMissingAmounts = memoryEvents
            .Where(x => x.EventTotal is null && x.MyShare is null)
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new
            {
                x.UserId,
                x.Id,
                x.Title,
                x.EventType,
                x.OccurredAt,
                participant_count = participants.Count(p => p.EventId == x.Id),
                settlement_count = settlements.Count(s => s.EventId == x.Id),
                currency = x.Currency
            })
            .ToList();

        var stats = new
        {
            generated_at_utc = DateTime.UtcNow,
            out_dir = outDir,
            user_filter = options.UserId,
            counts = new
            {
                entries = entries.Count,
                canonical_entities = entities.Count,
                entity_aliases = aliases.Count,
                entity_evidence = evidence.Count,
                memory_events = memoryEvents.Count,
                event_participants = participants.Count,
                settlements = settlements.Count
            },
            entities_by_kind = entitiesByKind,
            suspicious = new
            {
                pronoun_or_stopword_person_nodes = pronounPersons.Count,
                cross_kind_collisions = crossKindCollisions.Count,
                duplicate_within_kind = duplicateWithinKind.Count,
                event_like_entities = eventLikeEntities.Count,
                memory_events_missing_amounts = eventsMissingAmounts.Count
            }
        };

        var auditDir = Path.Combine(outDir, "audit");
        Directory.CreateDirectory(auditDir);

        await File.WriteAllTextAsync(Path.Combine(auditDir, "stats.json"), JsonSerializer.Serialize(stats, IndentedJson));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "cross_kind_collisions.json"), JsonSerializer.Serialize(crossKindCollisions.Take(200), IndentedJson));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "duplicate_within_kind.json"), JsonSerializer.Serialize(duplicateWithinKind.Take(200), IndentedJson));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "pronoun_person_nodes.json"), JsonSerializer.Serialize(pronounPersons.Take(200), IndentedJson));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "event_like_entities.json"), JsonSerializer.Serialize(eventLikeEntities.Take(200), IndentedJson));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "memory_events_missing_amounts.json"), JsonSerializer.Serialize(eventsMissingAmounts.Take(200), IndentedJson));

        var report = BuildMarkdownReport(stats);
        await File.WriteAllTextAsync(Path.Combine(auditDir, "report.md"), report);

        Console.WriteLine($"Audit written to: {auditDir}");
        return 0;
    }

    private static string BuildMarkdownReport(object stats)
    {
        // Keep report compact; details are in JSON next to it.
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(stats, IndentedJson));
        var root = doc.RootElement;
        var counts = root.GetProperty("counts");
        var suspicious = root.GetProperty("suspicious");

        var lines = new List<string>
        {
            "# Data Quality Audit",
            "",
            $"Generated at (UTC): `{root.GetProperty("generated_at_utc").GetDateTime():O}`",
            "",
            "## Counts",
            $"- Entries: **{counts.GetProperty("entries").GetInt32()}**",
            $"- CanonicalEntities: **{counts.GetProperty("canonical_entities").GetInt32()}**",
            $"- EntityAliases: **{counts.GetProperty("entity_aliases").GetInt32()}**",
            $"- EntityEvidence: **{counts.GetProperty("entity_evidence").GetInt32()}**",
            $"- MemoryEvents: **{counts.GetProperty("memory_events").GetInt32()}**",
            $"- EventParticipants: **{counts.GetProperty("event_participants").GetInt32()}**",
            $"- Settlements: **{counts.GetProperty("settlements").GetInt32()}**",
            "",
            "## Suspicious Signals (top-level)",
            $"- Pronoun/stopword PERSON nodes: **{suspicious.GetProperty("pronoun_or_stopword_person_nodes").GetInt32()}**",
            $"- Cross-kind collisions (same normalized name, different kinds): **{suspicious.GetProperty("cross_kind_collisions").GetInt32()}**",
            $"- Duplicate within kind (same normalized name, same kind): **{suspicious.GetProperty("duplicate_within_kind").GetInt32()}**",
            $"- Event-like canonical entities (title starts with 'Evento'): **{suspicious.GetProperty("event_like_entities").GetInt32()}**",
            $"- Memory events with missing amounts (EventTotal/MyShare): **{suspicious.GetProperty("memory_events_missing_amounts").GetInt32()}**",
            "",
            "## Next Steps",
            "- Fix extraction to avoid pronoun-as-person and money events without spend signal.",
            "- Run `Normalize Entities` (type + merge) for cross-kind collisions (place vs person).",
            "- Use feedback templates to block tokens and add force-link rules for stable roles.",
            "",
            "## Detail Files",
            "- `stats.json`",
            "- `cross_kind_collisions.json`",
            "- `duplicate_within_kind.json`",
            "- `pronoun_person_nodes.json`",
            "- `event_like_entities.json`",
            "- `memory_events_missing_amounts.json`",
            ""
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static async Task WriteJsonlAsync<T>(string path, IQueryable<T> query)
    {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream);

        await foreach (var row in query.AsAsyncEnumerable())
        {
            var json = JsonSerializer.Serialize(row, JsonlJson);
            await writer.WriteLineAsync(json);
        }
    }
}
