using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class OpenSearchProjectionService : ISearchProjectionService
{
    private readonly AppDbContext _db;
    private readonly IOpenSearchClient _client;
    private readonly SearchBackendOptions _options;
    private readonly ILogger<OpenSearchProjectionService> _logger;

    public OpenSearchProjectionService(
        AppDbContext db,
        IOpenSearchClient client,
        SearchBackendOptions options,
        ILogger<OpenSearchProjectionService> logger)
    {
        _db = db;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task ProjectEntryAsync(Entry entry, CancellationToken cancellationToken = default)
    {
        var doc = new EntrySearchDocument(
            entry.Id,
            entry.UserId,
            entry.Content,
            entry.CreatedAt,
            entry.UpdatedAt);

        var response = await _client.IndexAsync(doc, idx => idx
            .Index(_options.EntryIndex)
            .Id(entry.Id.ToString()), cancellationToken);

        if (!response.IsValid)
            _logger.LogWarning("Failed indexing entry {EntryId} in OpenSearch: {Error}", entry.Id, response.ServerError?.ToString());
    }

    public async Task DeleteEntryAsync(Guid entryId, Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<EntrySearchDocument>(entryId.ToString(), idx => idx.Index(_options.EntryIndex), cancellationToken);
        if (!response.IsValid && response.ServerError?.Status != 404)
            _logger.LogWarning("Failed deleting entry {EntryId} from OpenSearch: {Error}", entryId, response.ServerError?.ToString());
    }

    public async Task ProjectEntityAsync(CanonicalEntity entity, CancellationToken cancellationToken = default)
    {
        var aliasList = entity.Aliases
            .Select(x => x.Alias)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var relationHints = await _db.EventParticipants
            .Where(x => x.EntityId == entity.Id)
            .Include(x => x.Event)
            .Select(x => x.Event.Title)
            .Distinct()
            .Take(5)
            .ToListAsync(cancellationToken);

        var document = new EntitySearchDocument(
            entity.Id,
            entity.UserId,
            entity.Kind,
            entity.CanonicalName,
            entity.NormalizedCanonicalName,
            entity.AnchorKey,
            aliasList,
            entity.EntityCard,
            relationHints,
            entity.UpdatedAt);

        var response = await _client.IndexAsync(document, idx => idx
            .Index(_options.EntityIndex)
            .Id(entity.Id.ToString()), cancellationToken);

        if (!response.IsValid)
            _logger.LogWarning("Failed indexing entity {EntityId} in OpenSearch: {Error}", entity.Id, response.ServerError?.ToString());
    }

    public async Task DeleteEntityAsync(Guid entityId, Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<EntitySearchDocument>(entityId.ToString(), idx => idx.Index(_options.EntityIndex), cancellationToken);
        if (!response.IsValid && response.ServerError?.Status != 404)
            _logger.LogWarning("Failed deleting entity {EntityId} from OpenSearch: {Error}", entityId, response.ServerError?.ToString());
    }

    public async Task ProjectGoalItemAsync(GoalItem goalItem, CancellationToken cancellationToken = default)
    {
        var doc = new GoalSearchDocument(
            goalItem.Id,
            goalItem.UserId,
            goalItem.Title,
            goalItem.Description,
            goalItem.Status,
            goalItem.CreatedAt,
            goalItem.CompletedAt);

        var response = await _client.IndexAsync(doc, idx => idx
            .Index(_options.GoalIndex)
            .Id(goalItem.Id.ToString()), cancellationToken);

        if (!response.IsValid)
            _logger.LogWarning("Failed indexing goal {GoalId} in OpenSearch: {Error}", goalItem.Id, response.ServerError?.ToString());
    }

    public async Task DeleteGoalItemAsync(Guid goalItemId, Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<GoalSearchDocument>(goalItemId.ToString(), idx => idx.Index(_options.GoalIndex), cancellationToken);
        if (!response.IsValid && response.ServerError?.Status != 404)
            _logger.LogWarning("Failed deleting goal {GoalId} from OpenSearch: {Error}", goalItemId, response.ServerError?.ToString());
    }

    public async Task ResetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await DeleteByUserIdAsync(_options.EntryIndex, userId, cancellationToken);
        await DeleteByUserIdAsync(_options.EntityIndex, userId, cancellationToken);
        await DeleteByUserIdAsync(_options.GoalIndex, userId, cancellationToken);
    }

    private async Task DeleteByUserIdAsync(string indexName, Guid userId, CancellationToken cancellationToken)
    {
        var response = await _client.DeleteByQueryAsync<dynamic>(q => q
            .Index(indexName)
            .Query(query => query
                .Term(term => term
                    .Field("userId.keyword")
                    .Value(userId.ToString()))), cancellationToken);

        if (!response.IsValid && response.ServerError?.Status != 404)
            _logger.LogWarning("Failed delete-by-query for user {UserId} on index {Index}: {Error}", userId, indexName, response.ServerError?.ToString());
    }

    private sealed record EntrySearchDocument(
        Guid Id,
        Guid UserId,
        string Content,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private sealed record GoalSearchDocument(
        Guid Id,
        Guid UserId,
        string Title,
        string? Description,
        string Status,
        DateTime CreatedAt,
        DateTime? CompletedAt);

    private sealed record EntitySearchDocument(
        Guid Id,
        Guid UserId,
        string Kind,
        string CanonicalName,
        string NormalizedCanonicalName,
        string? AnchorKey,
        List<string> Aliases,
        string EntityCard,
        List<string> RelationHints,
        DateTime UpdatedAt);
}
