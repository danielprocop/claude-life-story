using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class EntityNormalizationService : IEntityNormalizationService
{
    private readonly AppDbContext _db;
    private readonly ISearchProjectionService _searchProjectionService;
    private readonly ILogger<EntityNormalizationService> _logger;

    public EntityNormalizationService(
        AppDbContext db,
        ISearchProjectionService searchProjectionService,
        ILogger<EntityNormalizationService> logger)
    {
        _db = db;
        _searchProjectionService = searchProjectionService;
        _logger = logger;
    }

    public async Task<NormalizeEntitiesResponse> NormalizeUserEntitiesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _db.CanonicalEntities
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
            return new NormalizeEntitiesResponse(0, 0, 0, 0, 0);

        var eventParticipantEntityIds = await _db.EventParticipants
            .Where(x => x.Event.UserId == userId)
            .Select(x => x.EntityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var settlementEntityIds = await _db.Settlements
            .Where(x => x.UserId == userId)
            .Select(x => x.CounterpartyEntityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var roleEvidenceEntityIds = await _db.EntityEvidence
            .Where(x =>
                x.Entity.UserId == userId &&
                (x.EvidenceType == "role_anchor" ||
                 x.PropertyName == "anchor" ||
                 x.MergeReason == "role_name_binding" ||
                 x.MergeReason == "parenthetical_role"))
            .Select(x => x.EntityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var strongPersonIds = entities
            .Where(x => x.Kind == "person")
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.AnchorKey) ||
                eventParticipantEntityIds.Contains(x.Id) ||
                settlementEntityIds.Contains(x.Id) ||
                roleEvidenceEntityIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSet();

        var placePersonGroups = entities
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedCanonicalName))
            .GroupBy(x => x.NormalizedCanonicalName)
            .Where(group =>
                group.Any(x => x.Kind == "place") &&
                group.Any(x => x.Kind == "person"))
            .ToList();

        var normalizedGroups = 0;
        var merged = 0;
        var suppressed = 0;
        var ambiguous = 0;
        var reindexed = 0;

        var touchedEntityIds = new HashSet<Guid>();
        var suppressedEntityIds = new HashSet<Guid>();
        var now = DateTime.UtcNow;

        foreach (var group in placePersonGroups)
        {
            normalizedGroups++;

            var placeTarget = group
                .Where(x => x.Kind == "place")
                .OrderByDescending(x => x.UpdatedAt)
                .First();

            var candidatePersons = group
                .Where(x => x.Kind == "person" && string.IsNullOrWhiteSpace(x.AnchorKey))
                .ToList();

            if (candidatePersons.Count == 0)
                continue;

            var hasStrongPerson = candidatePersons.Any(person => strongPersonIds.Contains(person.Id));
            if (hasStrongPerson)
            {
                ambiguous++;
                touchedEntityIds.Add(placeTarget.Id);
                continue;
            }

            foreach (var person in candidatePersons)
            {
                await MergeEntityIntoPlaceAsync(person.Id, placeTarget.Id, now, cancellationToken);

                person.Kind = "person_suppressed";
                person.Description = string.IsNullOrWhiteSpace(person.Description)
                    ? "suppressed_by_place_normalization"
                    : $"{person.Description};suppressed_by_place_normalization";
                person.UpdatedAt = now;
                suppressedEntityIds.Add(person.Id);
                merged++;
                suppressed++;
            }

            touchedEntityIds.Add(placeTarget.Id);
        }

        foreach (var touchedId in touchedEntityIds)
        {
            var entity = await _db.CanonicalEntities
                .Where(x => x.Id == touchedId)
                .Include(x => x.Aliases)
                .Include(x => x.Evidence)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
                continue;

            entity.EntityCard = BuildEntityCard(entity);
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var touchedId in touchedEntityIds)
        {
            var entity = await _db.CanonicalEntities
                .Where(x => x.Id == touchedId)
                .Include(x => x.Aliases)
                .Include(x => x.Evidence)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
                continue;

            await _searchProjectionService.ProjectEntityAsync(entity, cancellationToken);
            reindexed++;
        }

        foreach (var suppressedId in suppressedEntityIds)
        {
            await _searchProjectionService.DeleteEntityAsync(suppressedId, userId, cancellationToken);
            reindexed++;
        }

        _logger.LogInformation(
            "Entity normalization completed for user {UserId}. groups={Groups} merged={Merged} suppressed={Suppressed} ambiguous={Ambiguous}",
            userId,
            normalizedGroups,
            merged,
            suppressed,
            ambiguous);

        return new NormalizeEntitiesResponse(
            normalizedGroups,
            merged,
            suppressed,
            ambiguous,
            reindexed);
    }

    private async Task MergeEntityIntoPlaceAsync(Guid sourcePersonId, Guid targetPlaceId, DateTime now, CancellationToken cancellationToken)
    {
        var sourceAliases = await _db.EntityAliases
            .Where(x => x.EntityId == sourcePersonId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var targetAliases = await _db.EntityAliases
            .Where(x => x.EntityId == targetPlaceId)
            .Select(x => x.NormalizedAlias)
            .ToListAsync(cancellationToken);

        var targetAliasSet = targetAliases.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in sourceAliases)
        {
            if (targetAliasSet.Contains(alias.NormalizedAlias))
                continue;

            _db.EntityAliases.Add(new EntityAlias
            {
                Id = Guid.NewGuid(),
                EntityId = targetPlaceId,
                Alias = alias.Alias,
                NormalizedAlias = alias.NormalizedAlias,
                AliasType = "merged_alias",
                Confidence = Math.Clamp(alias.Confidence, 0.5f, 1f),
                CreatedAt = now
            });

            targetAliasSet.Add(alias.NormalizedAlias);
        }

        var sourceEvidence = await _db.EntityEvidence
            .Where(x => x.EntityId == sourcePersonId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var targetEvidence = await _db.EntityEvidence
            .Where(x => x.EntityId == targetPlaceId)
            .Select(x => new { x.EntryId, x.EvidenceType, x.Snippet })
            .ToListAsync(cancellationToken);

        var targetEvidenceSet = targetEvidence
            .Select(x => $"{x.EntryId}:{x.EvidenceType}:{x.Snippet}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var evidence in sourceEvidence)
        {
            var evidenceKey = $"{evidence.EntryId}:{evidence.EvidenceType}:{evidence.Snippet}";
            var alreadyPresent = targetEvidenceSet.Contains(evidenceKey);

            if (alreadyPresent)
                continue;

            _db.EntityEvidence.Add(new EntityEvidence
            {
                Id = Guid.NewGuid(),
                EntityId = targetPlaceId,
                EntryId = evidence.EntryId,
                EvidenceType = evidence.EvidenceType,
                Snippet = evidence.Snippet,
                PropertyName = evidence.PropertyName,
                Value = evidence.Value,
                MergeReason = string.IsNullOrWhiteSpace(evidence.MergeReason)
                    ? "normalize_place_person_merge"
                    : evidence.MergeReason,
                Confidence = evidence.Confidence,
                RecordedAt = evidence.RecordedAt
            });

            targetEvidenceSet.Add(evidenceKey);
        }
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
}
