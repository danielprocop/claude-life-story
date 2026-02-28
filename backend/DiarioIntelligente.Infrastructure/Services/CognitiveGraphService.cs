using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class CognitiveGraphService : ICognitiveGraphService
{
    private static readonly RoleAnchorDefinition[] RoleAnchors =
    {
        new("mother_of_user", "Madre", new[] { "mia madre", "madre", "mamma", "mia mamma" }),
        new("father_of_user", "Padre", new[] { "mio padre", "padre", "papà", "mio papà", "papa" }),
        new("brother_of_user", "Fratello", new[] { "mio fratello", "fratello" }),
        new("sister_of_user", "Sorella", new[] { "mia sorella", "sorella" }),
        new("wife_of_user", "Moglie", new[] { "mia moglie", "moglie" }),
        new("husband_of_user", "Marito", new[] { "mio marito", "marito" })
    };

    private static readonly string[] EventKeywords = { "cena", "pranzo", "spesa", "aperitivo", "uscita" };
    private static readonly Regex ParentheticalRoleRegex =
        new(@"(?<name>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]+)\s*\((?<role>[A-Za-zÀ-ÿ ]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CapitalizedTokenRegex =
        new(@"\b(?<name>[A-ZÀ-Ý][a-zà-ÿ'’\-]{2,})\b", RegexOptions.Compiled);
    private static readonly Regex AmountRegex =
        new(@"(?<amount>\d+(?:[.,]\d+)?)\s*(?:euro|€)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ISearchProjectionService _searchProjectionService;
    private readonly ILogger<CognitiveGraphService> _logger;

    public CognitiveGraphService(
        AppDbContext db,
        ISearchProjectionService searchProjectionService,
        ILogger<CognitiveGraphService> logger)
    {
        _db = db;
        _searchProjectionService = searchProjectionService;
        _logger = logger;
    }

    public async Task ProcessEntryAsync(Entry entry, AiAnalysisResult analysis, CancellationToken cancellationToken = default)
    {
        var entities = await _db.CanonicalEntities
            .Where(x => x.UserId == entry.UserId)
            .Include(x => x.Aliases)
            .Include(x => x.Evidence)
            .ToListAsync(cancellationToken);

        var changedEntities = new List<CanonicalEntity>();
        var roleContext = new Dictionary<string, CanonicalEntity>(StringComparer.OrdinalIgnoreCase);

        foreach (var mention in ExtractRoleMentions(entry.Content))
        {
            var entity = await GetOrCreateAnchorEntityAsync(entry.UserId, mention.Role.AnchorKey, mention.Role.DisplayName, entities, cancellationToken);
            AddAliasIfMissing(entity, mention.RawText, "role_phrase", 1.0f);
            AddEvidenceIfMissing(entity, entry, "role_anchor", mention.RawText, "anchor", mention.Role.AnchorKey, "role_anchor", 1.0f);
            roleContext[mention.Role.AnchorKey] = entity;
            MarkChanged(entity, changedEntities);
        }

        foreach (var binding in ExtractRoleNameBindings(entry.Content))
        {
            var entity = await GetOrCreateAnchorEntityAsync(entry.UserId, binding.Role.AnchorKey, binding.Role.DisplayName, entities, cancellationToken);
            ApplyCanonicalName(entity, binding.Name, binding.Role.DisplayName);
            AddAliasIfMissing(entity, binding.Role.PrimaryAlias, "role_phrase", 1.0f);
            AddEvidenceIfMissing(entity, entry, "name_assignment", binding.Snippet, "canonical_name", ToDisplayName(binding.Name), "role_name_binding", 0.98f);
            roleContext[binding.Role.AnchorKey] = entity;
            MarkChanged(entity, changedEntities);
        }

        foreach (var binding in ExtractParentheticalBindings(entry.Content))
        {
            var entity = await GetOrCreateAnchorEntityAsync(entry.UserId, binding.Role.AnchorKey, binding.Role.DisplayName, entities, cancellationToken);
            ApplyCanonicalName(entity, binding.Name, binding.Role.DisplayName);
            AddAliasIfMissing(entity, binding.Role.PrimaryAlias, "role_phrase", 1.0f);
            AddEvidenceIfMissing(entity, entry, "name_assignment", binding.Snippet, "canonical_name", ToDisplayName(binding.Name), "parenthetical_role", 0.99f);
            roleContext[binding.Role.AnchorKey] = entity;
            MarkChanged(entity, changedEntities);
        }

        var resolvedNameMentions = new Dictionary<string, CanonicalEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var personName in ExtractStandalonePersonMentions(entry.Content, analysis))
        {
            if (TryResolveRoleContextByNameHint(roleContext, personName, out var anchoredEntity))
            {
                ApplyCanonicalName(anchoredEntity, personName, anchoredEntity.CanonicalName);
                AddEvidenceIfMissing(anchoredEntity, entry, "alias_assignment", personName, "alias", ToDisplayName(personName), "role_context_match", 0.97f);
                resolvedNameMentions[personName] = anchoredEntity;
                MarkChanged(anchoredEntity, changedEntities);
                continue;
            }

            var entity = await ResolveOrCreatePersonEntityAsync(entry.UserId, personName, entities, entry, roleContext.Values.ToList(), cancellationToken);
            resolvedNameMentions[personName] = entity;
            MarkChanged(entity, changedEntities);
        }

        var eventSignal = ExtractEventSignal(entry.Content, analysis, resolvedNameMentions, roleContext);
        if (eventSignal != null)
        {
            var participants = new List<CanonicalEntity>();
            foreach (var participantRef in eventSignal.Participants)
            {
                if (participantRef.Entity != null)
                {
                    participants.Add(participantRef.Entity);
                    continue;
                }

                var resolved = await ResolveOrCreatePersonEntityAsync(
                    entry.UserId,
                    participantRef.RawName,
                    entities,
                    entry,
                    roleContext.Values.ToList(),
                    cancellationToken);

                participants.Add(resolved);
                MarkChanged(resolved, changedEntities);
            }

            if (eventSignal.Counterparty == null && !string.IsNullOrWhiteSpace(eventSignal.CounterpartyName))
            {
                var resolvedCounterparty = participants.FirstOrDefault(entity => IsNameMatch(entity, eventSignal.CounterpartyName!))
                    ?? await ResolveOrCreatePersonEntityAsync(
                        entry.UserId,
                        eventSignal.CounterpartyName!,
                        entities,
                        entry,
                        roleContext.Values.ToList(),
                        cancellationToken);

                if (participants.All(x => x.Id != resolvedCounterparty.Id))
                    participants.Add(resolvedCounterparty);

                eventSignal = eventSignal with
                {
                    Counterparty = resolvedCounterparty,
                    PayerEntityId = eventSignal.PayerEntityId ?? (
                        string.Equals(eventSignal.Notes, "explicit_debt_after_other_paid", StringComparison.OrdinalIgnoreCase)
                            ? resolvedCounterparty.Id
                            : eventSignal.PayerEntityId)
                };

                MarkChanged(resolvedCounterparty, changedEntities);
            }

            await UpsertEventAsync(entry, eventSignal, participants, entities, changedEntities, cancellationToken);
        }
        else
        {
            var paymentSignal = ExtractPaymentSignal(entry.Content);
            if (paymentSignal != null)
            {
                var counterparty = await ResolveOrCreatePersonEntityAsync(
                    entry.UserId,
                    paymentSignal.CounterpartyName,
                    entities,
                    entry,
                    roleContext.Values.ToList(),
                    cancellationToken);

                await ApplyPaymentAsync(entry, counterparty, paymentSignal, cancellationToken);
                MarkChanged(counterparty, changedEntities);
            }
        }

        foreach (var entity in changedEntities.DistinctBy(x => x.Id))
        {
            entity.EntityCard = BuildEntityCard(entity);
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var entity in changedEntities.DistinctBy(x => x.Id))
            await _searchProjectionService.ProjectEntityAsync(entity, cancellationToken);
    }

    public async Task ClearUserGraphAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entityIds = await _db.CanonicalEntities
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var eventIds = await _db.MemoryEvents
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var settlementIds = await _db.Settlements
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (settlementIds.Count > 0)
            await _db.SettlementPayments.Where(x => settlementIds.Contains(x.SettlementId)).ExecuteDeleteAsync(cancellationToken);

        await _db.Settlements.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        if (eventIds.Count > 0)
            await _db.EventParticipants.Where(x => eventIds.Contains(x.EventId)).ExecuteDeleteAsync(cancellationToken);

        await _db.MemoryEvents.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        if (entityIds.Count > 0)
        {
            await _db.EntityEvidence.Where(x => entityIds.Contains(x.EntityId)).ExecuteDeleteAsync(cancellationToken);
            await _db.EntityAliases.Where(x => entityIds.Contains(x.EntityId)).ExecuteDeleteAsync(cancellationToken);
        }

        await _db.CanonicalEntities.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<NodeViewResponse?> GetNodeViewAsync(Guid userId, Guid entityId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.CanonicalEntities
            .Where(x => x.UserId == userId && x.Id == entityId)
            .Include(x => x.Aliases)
            .Include(x => x.Evidence)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
            return null;

        var relations = new List<NodeRelationResponse>();
        if (!string.IsNullOrWhiteSpace(entity.AnchorKey))
            relations.Add(new NodeRelationResponse("role_anchor", entity.AnchorKey));

        PersonNodeViewResponse? personView = null;
        EventNodeViewResponse? eventView = null;

        if (entity.Kind == "person")
        {
            var events = await _db.EventParticipants
                .Where(x => x.EntityId == entityId)
                .Include(x => x.Event)
                .ThenInclude(x => x.Entity)
                .ToListAsync(cancellationToken);

            var settlements = await _db.Settlements
                .Where(x => x.UserId == userId && x.CounterpartyEntityId == entityId)
                .Include(x => x.Event)
                .ThenInclude(x => x!.Entity)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            personView = new PersonNodeViewResponse(
                settlements.Where(x => x.Direction == "user_owes" && x.Status != "settled").Sum(x => x.RemainingAmount),
                settlements.Where(x => x.Direction == "owes_user" && x.Status != "settled").Sum(x => x.RemainingAmount),
                events.Select(x => new PersonEventSummaryResponse(
                        x.Event.EntityId,
                        x.Event.Title,
                        x.Event.EventType,
                        x.Event.OccurredAt,
                        x.Event.EventTotal,
                        x.Event.MyShare))
                    .OrderByDescending(x => x.OccurredAt)
                    .ToList(),
                settlements.Select(MapSettlement).ToList());
        }
        else if (entity.Kind == "event")
        {
            var memoryEvent = await _db.MemoryEvents
                .Where(x => x.UserId == userId && x.EntityId == entityId)
                .Include(x => x.Participants)
                .ThenInclude(x => x.Entity)
                .Include(x => x.Settlements)
                .OrderByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (memoryEvent != null)
            {
                eventView = new EventNodeViewResponse(
                    memoryEvent.EventType,
                    memoryEvent.Title,
                    memoryEvent.OccurredAt,
                    memoryEvent.EventTotal,
                    memoryEvent.MyShare,
                    memoryEvent.Currency,
                    memoryEvent.IncludesUser,
                    memoryEvent.Participants.Select(x => new EventParticipantResponse(x.EntityId, x.Entity.CanonicalName, x.Entity.AnchorKey, x.Role)).ToList(),
                    memoryEvent.Settlements.Select(MapSettlement).ToList(),
                    memoryEvent.SourceEntryId);
            }
        }

        return new NodeViewResponse(
            entity.Id,
            entity.Kind,
            entity.CanonicalName,
            entity.AnchorKey,
            entity.Aliases.Select(x => x.Alias).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            relations,
            entity.Evidence.OrderByDescending(x => x.RecordedAt).Select(x => new NodeEvidenceResponse(x.EntryId, x.EvidenceType, x.Snippet, x.RecordedAt, x.MergeReason)).ToList(),
            personView,
            eventView);
    }

    private async Task UpsertEventAsync(
        Entry entry,
        EventSignal eventSignal,
        List<CanonicalEntity> participants,
        List<CanonicalEntity> entities,
        List<CanonicalEntity> changedEntities,
        CancellationToken cancellationToken)
    {
        var existingEvent = await _db.MemoryEvents
            .Where(x => x.UserId == entry.UserId && x.SourceEntryId == entry.Id)
            .Include(x => x.Participants)
            .Include(x => x.Entity)
            .FirstOrDefaultAsync(cancellationToken);

        var title = BuildEventTitle(eventSignal, entry.CreatedAt);
        var eventEntity = existingEvent?.Entity
            ?? await CreateEntityAsync(entry.UserId, "event", title, null, entities, cancellationToken);

        eventEntity.CanonicalName = title;
        eventEntity.NormalizedCanonicalName = Normalize(title);
        eventEntity.Description = $"Evento {eventSignal.EventType}";
        AddEvidenceIfMissing(eventEntity, entry, "event", eventSignal.SourceSnippet, "event_type", eventSignal.EventType, "event_extraction", 0.92f);
        MarkChanged(eventEntity, changedEntities);

        var memoryEvent = existingEvent ?? new MemoryEvent
        {
            Id = Guid.NewGuid(),
            UserId = entry.UserId,
            EntityId = eventEntity.Id,
            SourceEntryId = entry.Id,
            CreatedAt = DateTime.UtcNow
        };

        memoryEvent.EventType = eventSignal.EventType;
        memoryEvent.Title = title;
        memoryEvent.OccurredAt = entry.CreatedAt;
        memoryEvent.EventTotal = eventSignal.EventTotal;
        memoryEvent.MyShare = eventSignal.MyShare;
        memoryEvent.Currency = eventSignal.Currency;
        memoryEvent.Notes = eventSignal.Notes;
        memoryEvent.SourceSnippet = eventSignal.SourceSnippet;
        memoryEvent.IncludesUser = true;
        memoryEvent.UpdatedAt = DateTime.UtcNow;

        if (existingEvent == null)
            _db.MemoryEvents.Add(memoryEvent);

        if (existingEvent != null)
        {
            _db.EventParticipants.RemoveRange(existingEvent.Participants);
            existingEvent.Participants.Clear();
        }

        var resolvedCounterparty = eventSignal.Counterparty ?? (participants.Count == 1 ? participants[0] : null);
        var payerEntityId = eventSignal.PayerEntityId;
        if (!payerEntityId.HasValue &&
            resolvedCounterparty != null &&
            string.Equals(eventSignal.Notes, "explicit_debt_after_other_paid", StringComparison.OrdinalIgnoreCase))
        {
            payerEntityId = resolvedCounterparty.Id;
        }

        foreach (var participant in participants.DistinctBy(x => x.Id))
        {
            _db.EventParticipants.Add(new EventParticipant
            {
                EventId = memoryEvent.Id,
                EntityId = participant.Id,
                Role = payerEntityId == participant.Id ? "payer" : "participant"
            });
        }

        if (eventSignal.SettlementAmount.HasValue && resolvedCounterparty != null)
        {
            var settlement = await _db.Settlements
                .FirstOrDefaultAsync(x =>
                    x.UserId == entry.UserId &&
                    x.SourceEntryId == entry.Id &&
                    x.CounterpartyEntityId == resolvedCounterparty.Id &&
                    x.Direction == eventSignal.SettlementDirection &&
                    x.OriginalAmount == eventSignal.SettlementAmount.Value,
                    cancellationToken);

            if (settlement == null)
            {
                settlement = new Settlement
                {
                    Id = Guid.NewGuid(),
                    UserId = entry.UserId,
                    EventId = memoryEvent.Id,
                    CounterpartyEntityId = resolvedCounterparty.Id,
                    SourceEntryId = entry.Id,
                    Direction = eventSignal.SettlementDirection,
                    OriginalAmount = eventSignal.SettlementAmount.Value,
                    RemainingAmount = eventSignal.SettlementAmount.Value,
                    Currency = eventSignal.Currency,
                    Status = "open",
                    Notes = eventSignal.Notes,
                    SourceSnippet = eventSignal.SourceSnippet,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Settlements.Add(settlement);
            }
        }
    }

    private async Task ApplyPaymentAsync(Entry entry, CanonicalEntity counterparty, PaymentSignal paymentSignal, CancellationToken cancellationToken)
    {
        var directionToClose = paymentSignal.Direction switch
        {
            "user_paid_counterparty" => "user_owes",
            "counterparty_paid_user" => "owes_user",
            _ => "user_owes"
        };

        var candidateSettlements = await _db.Settlements
            .Where(x =>
                x.UserId == entry.UserId &&
                x.CounterpartyEntityId == counterparty.Id &&
                x.Direction == directionToClose &&
                x.Status != "settled" &&
                x.Currency == paymentSignal.Currency)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (candidateSettlements.Count == 0)
            return;

        var matchingRemaining = candidateSettlements
            .Where(x => x.RemainingAmount == paymentSignal.Amount)
            .ToList();

        Settlement? settlement = matchingRemaining.Count switch
        {
            1 => matchingRemaining[0],
            > 1 => matchingRemaining
                .Where(x => x.OriginalAmount == paymentSignal.Amount)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(),
            _ => null
        };

        if (settlement == null)
        {
            var fittingCandidates = candidateSettlements
                .Where(x => x.RemainingAmount >= paymentSignal.Amount)
                .OrderBy(x => Math.Abs(x.RemainingAmount - paymentSignal.Amount))
                .ThenByDescending(x => x.CreatedAt)
                .ToList();

            if (fittingCandidates.Count == 1)
            {
                settlement = fittingCandidates[0];
            }
            else if (fittingCandidates.Count > 1)
            {
                var smallestDelta = Math.Abs(fittingCandidates[0].RemainingAmount - paymentSignal.Amount);
                var tied = fittingCandidates
                    .Where(x => Math.Abs(x.RemainingAmount - paymentSignal.Amount) == smallestDelta)
                    .ToList();

                settlement = tied.Count == 1 ? tied[0] : null;
            }
        }

        if (settlement == null)
        {
            _logger.LogWarning(
                "Skipped payment mapping for entry {EntryId}: ambiguous settlement candidates for counterparty {CounterpartyId}, direction {Direction}, amount {Amount}",
                entry.Id,
                counterparty.Id,
                directionToClose,
                paymentSignal.Amount);
            return;
        }

        var existingPayment = await _db.SettlementPayments
            .FirstOrDefaultAsync(x =>
                x.SettlementId == settlement.Id &&
                x.EntryId == entry.Id &&
                x.Amount == paymentSignal.Amount,
                cancellationToken);

        if (existingPayment != null)
            return;

        settlement.RemainingAmount = Math.Max(0, settlement.RemainingAmount - paymentSignal.Amount);
        settlement.Status = settlement.RemainingAmount == 0
            ? "settled"
            : settlement.RemainingAmount < settlement.OriginalAmount
                ? "partially_paid"
                : "open";
        settlement.UpdatedAt = DateTime.UtcNow;

        _db.SettlementPayments.Add(new SettlementPayment
        {
            Id = Guid.NewGuid(),
            SettlementId = settlement.Id,
            EntryId = entry.Id,
            Amount = paymentSignal.Amount,
            Snippet = paymentSignal.SourceSnippet,
            RecordedAt = entry.CreatedAt
        });
    }

    private async Task<CanonicalEntity> ResolveOrCreatePersonEntityAsync(
        Guid userId,
        string rawName,
        List<CanonicalEntity> entities,
        Entry entry,
        List<CanonicalEntity> roleEntities,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(rawName);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Cannot resolve empty entity name.");

        var exact = entities
            .Where(x => x.Kind == "person")
            .FirstOrDefault(x =>
                x.NormalizedCanonicalName == normalized ||
                x.Aliases.Any(a => a.NormalizedAlias == normalized));

        if (exact != null)
        {
            AddAliasIfMissing(exact, rawName, "observed_name", 0.95f);
            AddEvidenceIfMissing(exact, entry, "mention", rawName, "mention", rawName, "exact_alias", 0.95f);
            return exact;
        }

        var ranked = entities
            .Where(x => x.Kind == "person")
            .Select(x => new
            {
                Entity = x,
                Score = Math.Max(
                    JaroWinklerSimilarity(normalized, x.NormalizedCanonicalName),
                    x.Aliases.Select(a => JaroWinklerSimilarity(normalized, a.NormalizedAlias)).DefaultIfEmpty(0).Max())
            })
            .Where(x => x.Score >= 0.9)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (ranked.Count > 0 && (ranked.Count == 1 || ranked[0].Score - ranked[1].Score >= 0.04))
        {
            AddAliasIfMissing(ranked[0].Entity, rawName, "observed_typo", 0.9f);
            AddEvidenceIfMissing(ranked[0].Entity, entry, "merge", rawName, "alias", rawName, "fuzzy_alias", (float)ranked[0].Score);
            return ranked[0].Entity;
        }

        foreach (var roleEntity in roleEntities)
        {
            if (JaroWinklerSimilarity(normalized, roleEntity.NormalizedCanonicalName) >= 0.9)
            {
                AddAliasIfMissing(roleEntity, rawName, "observed_typo", 0.91f);
                AddEvidenceIfMissing(roleEntity, entry, "merge", rawName, "alias", rawName, "role_anchor_fuzzy", 0.91f);
                return roleEntity;
            }
        }

        var created = await CreateEntityAsync(userId, "person", ToDisplayName(rawName), null, entities, cancellationToken);
        AddAliasIfMissing(created, rawName, "canonical_name", 1.0f);
        AddEvidenceIfMissing(created, entry, "mention", rawName, "canonical_name", rawName, "new_person", 0.8f);
        return created;
    }

    private async Task<CanonicalEntity> GetOrCreateAnchorEntityAsync(
        Guid userId,
        string anchorKey,
        string displayName,
        List<CanonicalEntity> entities,
        CancellationToken cancellationToken)
    {
        var existing = entities.FirstOrDefault(x => x.UserId == userId && x.AnchorKey == anchorKey);
        if (existing != null)
            return existing;

        var created = await CreateEntityAsync(userId, "person", displayName, anchorKey, entities, cancellationToken);
        AddAliasIfMissing(created, displayName, "role_phrase", 1.0f);
        return created;
    }

    private async Task<CanonicalEntity> CreateEntityAsync(
        Guid userId,
        string kind,
        string canonicalName,
        string? anchorKey,
        List<CanonicalEntity> entities,
        CancellationToken cancellationToken)
    {
        var entity = new CanonicalEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            CanonicalName = canonicalName,
            NormalizedCanonicalName = Normalize(canonicalName),
            AnchorKey = anchorKey,
            EntityCard = canonicalName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CanonicalEntities.Add(entity);
        entities.Add(entity);
        await Task.CompletedTask;
        return entity;
    }

    private void ApplyCanonicalName(CanonicalEntity entity, string rawName, string defaultAnchorDisplayName)
    {
        var pretty = ToDisplayName(rawName);
        var normalized = Normalize(rawName);

        if (string.IsNullOrWhiteSpace(entity.CanonicalName) ||
            string.Equals(entity.CanonicalName, defaultAnchorDisplayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entity.NormalizedCanonicalName, Normalize(defaultAnchorDisplayName), StringComparison.Ordinal))
        {
            entity.CanonicalName = pretty;
            entity.NormalizedCanonicalName = normalized;
        }

        AddAliasIfMissing(entity, rawName, "observed_name", 1.0f);
    }

    private void AddAliasIfMissing(CanonicalEntity entity, string rawAlias, string aliasType, float confidence)
    {
        var normalizedAlias = Normalize(rawAlias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
            return;

        if (entity.Aliases.Any(a => a.NormalizedAlias == normalizedAlias))
            return;

        var alias = new EntityAlias
        {
            Id = Guid.NewGuid(),
            EntityId = entity.Id,
            Entity = entity,
            Alias = ToDisplayName(rawAlias),
            NormalizedAlias = normalizedAlias,
            AliasType = aliasType,
            Confidence = confidence,
            CreatedAt = DateTime.UtcNow
        };

        entity.Aliases.Add(alias);
        _db.EntityAliases.Add(alias);
    }

    private void AddEvidenceIfMissing(
        CanonicalEntity entity,
        Entry entry,
        string evidenceType,
        string snippet,
        string? propertyName,
        string? value,
        string mergeReason,
        float confidence)
    {
        if (string.IsNullOrWhiteSpace(snippet))
            return;

        if (entity.Evidence.Any(x => x.EntryId == entry.Id && x.EvidenceType == evidenceType && x.Snippet == snippet))
            return;

        var evidence = new EntityEvidence
        {
            Id = Guid.NewGuid(),
            EntityId = entity.Id,
            Entity = entity,
            EntryId = entry.Id,
            Entry = entry,
            EvidenceType = evidenceType,
            Snippet = snippet.Length > 300 ? snippet[..300] : snippet,
            PropertyName = propertyName,
            Value = value,
            MergeReason = mergeReason,
            Confidence = confidence,
            RecordedAt = entry.CreatedAt
        };

        entity.Evidence.Add(evidence);
        _db.EntityEvidence.Add(evidence);
    }

    private static void MarkChanged(CanonicalEntity entity, List<CanonicalEntity> changedEntities)
    {
        if (changedEntities.All(x => x.Id != entity.Id))
            changedEntities.Add(entity);
    }

    private static IEnumerable<RoleMention> ExtractRoleMentions(string content)
    {
        foreach (var role in RoleAnchors)
        {
            foreach (var alias in role.Aliases)
            {
                var matches = Regex.Matches(content, $@"\b{Regex.Escape(alias)}\b", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                    yield return new RoleMention(role, match.Value);
            }
        }
    }

    private static IEnumerable<RoleNameBinding> ExtractRoleNameBindings(string content)
    {
        foreach (var role in RoleAnchors)
        {
            foreach (var alias in role.Aliases)
            {
                var regex = new Regex(
                    $@"\b{Regex.Escape(alias)}\b[^.!?\n]{{0,40}}?\bsi chiama\b\s+(?<name>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]+)",
                    RegexOptions.IgnoreCase);

                foreach (Match match in regex.Matches(content))
                    yield return new RoleNameBinding(role, match.Groups["name"].Value, match.Value);
            }
        }
    }

    private static IEnumerable<RoleNameBinding> ExtractParentheticalBindings(string content)
    {
        foreach (Match match in ParentheticalRoleRegex.Matches(content))
        {
            var role = FindRoleByAlias(match.Groups["role"].Value);
            if (role != null)
                yield return new RoleNameBinding(role, match.Groups["name"].Value, match.Value);
        }
    }

    private static IEnumerable<string> ExtractStandalonePersonMentions(string content, AiAnalysisResult analysis)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var concept in analysis.Concepts.Where(x => x.Type == "person"))
        {
            if (!string.IsNullOrWhiteSpace(concept.Label) && seen.Add(concept.Label.Trim()))
                yield return concept.Label.Trim();
        }

        foreach (Match match in CapitalizedTokenRegex.Matches(content))
        {
            var token = match.Groups["name"].Value.Trim();
            if (token.Length < 3 || IsCommonNonNameToken(token))
                continue;

            if (seen.Add(token))
                yield return token;
        }
    }

    private static bool TryResolveRoleContextByNameHint(
        Dictionary<string, CanonicalEntity> roleContext,
        string personName,
        out CanonicalEntity entity)
    {
        var normalized = Normalize(personName);
        foreach (var candidate in roleContext.Values)
        {
            if (JaroWinklerSimilarity(normalized, candidate.NormalizedCanonicalName) >= 0.9)
            {
                entity = candidate;
                return true;
            }
        }

        entity = null!;
        return false;
    }

    private static EventSignal? ExtractEventSignal(
        string content,
        AiAnalysisResult analysis,
        Dictionary<string, CanonicalEntity> resolvedNames,
        Dictionary<string, CanonicalEntity> roleContext)
    {
        var lowered = content.ToLowerInvariant();
        var eventType = EventKeywords.FirstOrDefault(lowered.Contains);
        var hasFinancialSignal = lowered.Contains("ho speso") ||
                                 lowered.Contains("abbiamo speso") ||
                                 lowered.Contains("spesa") ||
                                 lowered.Contains("devo ") ||
                                 lowered.Contains("mi deve");

        if (eventType == null && !hasFinancialSignal)
            return null;

        var participants = ExtractParticipants(content, resolvedNames, roleContext).ToList();
        var amounts = AmountRegex.Matches(content)
            .Select(x => ParseDecimal(x.Groups["amount"].Value))
            .ToList();

        var settlement = ExtractSettlementSignal(content, participants);
        var eventTotal = ExtractEventTotal(content) ?? amounts.FirstOrDefault(x => x.HasValue);
        var participantCountIncludingUser = 1 + participants.Count;
        decimal? myShare = null;

        if (settlement?.Amount is decimal settlementAmount)
        {
            if (lowered.Contains("ha pagato lui") || lowered.Contains("ha pagato lei"))
                myShare = settlementAmount;
            else if (eventTotal.HasValue && participantCountIncludingUser == 2 && settlementAmount <= eventTotal.Value)
                myShare = settlementAmount;
        }

        if (!myShare.HasValue && eventTotal.HasValue && participantCountIncludingUser > 0)
            myShare = Math.Round(eventTotal.Value / participantCountIncludingUser, 2, MidpointRounding.AwayFromZero);

        return new EventSignal(
            eventType ?? "expense",
            "EUR",
            eventTotal,
            myShare,
            settlement?.Amount,
            settlement?.Direction ?? "user_owes",
            settlement?.Counterparty,
            settlement?.CounterpartyName,
            settlement?.PayerEntityId,
            participants,
            content.Length > 220 ? content[..220] : content,
            settlement?.Notes);
    }

    private static SettlementSignal? ExtractSettlementSignal(string content, List<ParticipantRef> participants)
    {
        var lower = content.ToLowerInvariant();
        var explicitOwe = Regex.Match(
            content,
            @"devo(?:\s+(?:dare|darli|dargli|darle|ridare|restituire))?\s+(?<amount>\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase);

        if (explicitOwe.Success)
        {
            var amount = ParseDecimal(explicitOwe.Groups["amount"].Value);
            var targetMatch = Regex.Match(
                content,
                @"devo(?:\s+(?:dare|darli|dargli|darle|ridare|restituire))?\s+\d+(?:[.,]\d+)?(?:\s*(?:euro|€))?\s+(?:a|ad)\s+(?<name>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]+)",
                RegexOptions.IgnoreCase);
            var targetName = targetMatch.Success ? targetMatch.Groups["name"].Value : null;
            var counterparty = targetMatch.Success
                ? participants.FirstOrDefault(x => Normalize(x.RawName) == Normalize(targetMatch.Groups["name"].Value))
                : participants.FirstOrDefault();

            return new SettlementSignal(
                amount,
                "user_owes",
                counterparty?.Entity,
                targetName,
                counterparty?.Entity?.Id,
                lower.Contains("ha pagato lui") || lower.Contains("ha pagato lei") ? "explicit_debt_after_other_paid" : "explicit_debt");
        }

        var explicitCredit = Regex.Match(content, @"mi deve\s+(?<amount>\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
        if (explicitCredit.Success)
        {
            var amount = ParseDecimal(explicitCredit.Groups["amount"].Value);
            var targetMatch = Regex.Match(
                content,
                @"mi deve\s+\d+(?:[.,]\d+)?(?:\s*(?:euro|€))?\s+(?:da|di)\s+(?<name>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]+)",
                RegexOptions.IgnoreCase);
            var targetName = targetMatch.Success ? targetMatch.Groups["name"].Value : null;
            var counterparty = targetMatch.Success
                ? participants.FirstOrDefault(x => Normalize(x.RawName) == Normalize(targetMatch.Groups["name"].Value))
                : participants.FirstOrDefault();

            return new SettlementSignal(amount, "owes_user", counterparty?.Entity, targetName, null, "explicit_credit");
        }

        return null;
    }

    private static PaymentSignal? ExtractPaymentSignal(string content)
    {
        var outgoing = Regex.Match(
            content,
            @"ho dato\s+(?<amount>\d+(?:[.,]\d+)?)\s*(?:euro|€)?\s+a(?:d)?\s+(?<name>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]+)",
            RegexOptions.IgnoreCase);

        if (outgoing.Success)
            return new PaymentSignal(
                ParseDecimal(outgoing.Groups["amount"].Value) ?? 0,
                outgoing.Groups["name"].Value,
                "user_paid_counterparty",
                outgoing.Value,
                "EUR");

        var incoming = Regex.Match(
            content,
            @"(?<name>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]+)\s+mi ha dato\s+(?<amount>\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase);

        if (incoming.Success)
            return new PaymentSignal(
                ParseDecimal(incoming.Groups["amount"].Value) ?? 0,
                incoming.Groups["name"].Value,
                "counterparty_paid_user",
                incoming.Value,
                "EUR");

        return null;
    }

    private static IEnumerable<ParticipantRef> ExtractParticipants(
        string content,
        Dictionary<string, CanonicalEntity> resolvedNames,
        Dictionary<string, CanonicalEntity> roleContext)
    {
        var results = new List<ParticipantRef>();
        var withMatch = Regex.Match(
            content,
            @"\bcon\s+(?<participants>.+?)(?=(?:\s+(?:e|ed)\s+(?:ho|ha|abbiamo|devo|mi|ci|poi)\b)|[,.!?]|$)",
            RegexOptions.IgnoreCase);

        if (!withMatch.Success)
            return results;

        var rawParticipants = withMatch.Groups["participants"].Value
            .Split(new[] { ",", " e " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var raw in rawParticipants)
        {
            var parenthetical = ParentheticalRoleRegex.Match(raw);
            if (parenthetical.Success)
            {
                var role = FindRoleByAlias(parenthetical.Groups["role"].Value);
                var entity = role != null && roleContext.TryGetValue(role.AnchorKey, out var anchored) ? anchored : null;
                results.Add(new ParticipantRef(parenthetical.Groups["name"].Value, entity));
                continue;
            }

            var roleDefinition = FindRoleByAlias(raw);
            if (roleDefinition != null && roleContext.TryGetValue(roleDefinition.AnchorKey, out var roleEntity))
            {
                results.Add(new ParticipantRef(raw, roleEntity));
                continue;
            }

            var cleaned = raw.Trim();
            if (resolvedNames.TryGetValue(cleaned, out var resolvedEntity))
            {
                results.Add(new ParticipantRef(cleaned, resolvedEntity));
                continue;
            }

            results.Add(new ParticipantRef(cleaned, null));
        }

        return results;
    }

    private static decimal? ExtractEventTotal(string content)
    {
        var match = Regex.Match(
            content,
            @"(?:ho speso|abbiamo speso|spesa(?: totale)?|totale(?: della cena)?(?: era)?)\s+(?<amount>\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase);

        return match.Success ? ParseDecimal(match.Groups["amount"].Value) : null;
    }

    private static RoleAnchorDefinition? FindRoleByAlias(string rawAlias)
    {
        var normalized = Normalize(rawAlias);
        return RoleAnchors.FirstOrDefault(x => x.Aliases.Any(alias => Normalize(alias) == normalized));
    }

    private static string BuildEventTitle(EventSignal signal, DateTime occurredAt)
    {
        var prefix = signal.EventType switch
        {
            "cena" => "Cena",
            "pranzo" => "Pranzo",
            "spesa" => "Spesa",
            "aperitivo" => "Aperitivo",
            "uscita" => "Uscita",
            _ => "Evento"
        };

        return $"{prefix} {occurredAt:yyyy-MM-dd}";
    }

    private static string BuildEntityCard(CanonicalEntity entity)
    {
        var aliases = entity.Aliases
            .Select(x => x.Alias)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var parts = new List<string> { entity.CanonicalName };
        if (!string.IsNullOrWhiteSpace(entity.AnchorKey))
            parts.Add(entity.AnchorKey);
        if (aliases.Count > 0)
            parts.Add($"alias: {string.Join(", ", aliases)}");
        if (!string.IsNullOrWhiteSpace(entity.Description))
            parts.Add(entity.Description);
        return string.Join(" | ", parts);
    }

    private static bool IsNameMatch(CanonicalEntity entity, string rawName)
    {
        var normalized = Normalize(rawName);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (entity.NormalizedCanonicalName == normalized)
            return true;

        return entity.Aliases.Any(alias => alias.NormalizedAlias == normalized);
    }

    private static SettlementSummaryResponse MapSettlement(Settlement settlement)
    {
        return new SettlementSummaryResponse(
            settlement.Id,
            settlement.Direction,
            settlement.OriginalAmount,
            settlement.RemainingAmount,
            settlement.Currency,
            settlement.Status,
            settlement.CreatedAt,
            settlement.Event?.EntityId,
            settlement.Event?.Title);
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string ToDisplayName(string value)
    {
        var cleaned = value.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return cleaned;

        var textInfo = CultureInfo.GetCultureInfo("it-IT").TextInfo;
        return textInfo.ToTitleCase(cleaned.ToLowerInvariant());
    }

    private static bool IsCommonNonNameToken(string token)
    {
        return token is "Oggi" or "Ieri" or "Domani" or "Sono" or "Siamo" or "Abbiamo" or "Ho" or "Poi" or "Con" or "Mia" or "Mio";
    }

    private static decimal? ParseDecimal(string raw)
    {
        if (decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static double JaroWinklerSimilarity(string left, string right)
    {
        if (left == right)
            return 1;
        if (left.Length == 0 || right.Length == 0)
            return 0;

        var matchDistance = Math.Max(left.Length, right.Length) / 2 - 1;
        var leftMatches = new bool[left.Length];
        var rightMatches = new bool[right.Length];

        var matches = 0;
        for (var i = 0; i < left.Length; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(i + matchDistance + 1, right.Length);
            for (var j = start; j < end; j++)
            {
                if (rightMatches[j] || left[i] != right[j])
                    continue;

                leftMatches[i] = true;
                rightMatches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0)
            return 0;

        var transpositions = 0;
        for (int i = 0, k = 0; i < left.Length; i++)
        {
            if (!leftMatches[i])
                continue;

            while (!rightMatches[k]) k++;
            if (left[i] != right[k]) transpositions++;
            k++;
        }

        var m = matches;
        var jaro = (m / (double)left.Length + m / (double)right.Length + (m - transpositions / 2.0) / m) / 3.0;
        var prefix = 0;
        for (var i = 0; i < Math.Min(4, Math.Min(left.Length, right.Length)); i++)
        {
            if (left[i] != right[i])
                break;
            prefix++;
        }

        return jaro + prefix * 0.1 * (1 - jaro);
    }

    private sealed record RoleAnchorDefinition(string AnchorKey, string DisplayName, string[] Aliases)
    {
        public string PrimaryAlias => Aliases[0];
    }

    private sealed record RoleMention(RoleAnchorDefinition Role, string RawText);
    private sealed record RoleNameBinding(RoleAnchorDefinition Role, string Name, string Snippet);
    private sealed record ParticipantRef(string RawName, CanonicalEntity? Entity);

    private sealed record SettlementSignal(
        decimal? Amount,
        string Direction,
        CanonicalEntity? Counterparty,
        string? CounterpartyName,
        Guid? PayerEntityId,
        string Notes);

    private sealed record EventSignal(
        string EventType,
        string Currency,
        decimal? EventTotal,
        decimal? MyShare,
        decimal? SettlementAmount,
        string SettlementDirection,
        CanonicalEntity? Counterparty,
        string? CounterpartyName,
        Guid? PayerEntityId,
        List<ParticipantRef> Participants,
        string SourceSnippet,
        string? Notes);

    private sealed record PaymentSignal(
        decimal Amount,
        string CounterpartyName,
        string Direction,
        string SourceSnippet,
        string Currency);
}
