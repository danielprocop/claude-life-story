using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class OpenSearchEntityRetrievalService : IEntityRetrievalService
{
    private readonly IOpenSearchClient _client;
    private readonly SearchBackendOptions _options;
    private readonly ILogger<OpenSearchEntityRetrievalService> _logger;

    public OpenSearchEntityRetrievalService(
        IOpenSearchClient client,
        SearchBackendOptions options,
        ILogger<OpenSearchEntityRetrievalService> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<List<EntityRetrievalCandidate>> SearchEntityCandidatesAsync(
        Guid userId,
        string query,
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<EntityRetrievalCandidate>();

        try
        {
            var response = await _client.SearchAsync<EntitySearchDocument>(s => s
                .Index(_options.EntityIndex)
                .Size(Math.Clamp(limit, 1, 50))
                .Query(q => q
                    .Bool(boolQuery => boolQuery
                        .Filter(filter => filter
                            .Term(term => term
                                .Field(field => field.UserId)
                                .Value(userId)))
                        .Must(must => must
                            .MultiMatch(match => match
                                .Query(query)
                                .Fields(fields => fields
                                    .Field(field => field.CanonicalName, 4.0)
                                    .Field(field => field.Aliases, 3.0)
                                    .Field(field => field.AnchorKey, 2.0)
                                    .Field(field => field.EntityCard, 1.5)
                                    .Field(field => field.RelationHints, 1.0))
                                .Fuzziness(Fuzziness.Auto))))), cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogWarning("OpenSearch candidate retrieval failed for user {UserId}: {Error}", userId, response.ServerError?.ToString());
                return new List<EntityRetrievalCandidate>();
            }

            return response.Hits
                .Where(hit => hit.Source?.Id != Guid.Empty)
                .Select(hit => new EntityRetrievalCandidate(hit.Source!.Id, (float)(hit.Score ?? 0d)))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenSearch candidate retrieval exception for user {UserId}", userId);
            return new List<EntityRetrievalCandidate>();
        }
    }

    private sealed class EntitySearchDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string CanonicalName { get; set; } = string.Empty;
        public string? AnchorKey { get; set; }
        public List<string> Aliases { get; set; } = new();
        public string EntityCard { get; set; } = string.Empty;
        public List<string> RelationHints { get; set; } = new();
    }
}
