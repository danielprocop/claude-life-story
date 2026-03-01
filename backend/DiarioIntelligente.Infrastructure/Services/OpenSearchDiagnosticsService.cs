using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class OpenSearchDiagnosticsService : ISearchDiagnosticsService
{
    private readonly IOpenSearchClient _client;
    private readonly SearchBackendOptions _options;
    private readonly ILogger<OpenSearchDiagnosticsService> _logger;

    public OpenSearchDiagnosticsService(
        IOpenSearchClient client,
        SearchBackendOptions options,
        ILogger<OpenSearchDiagnosticsService> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<SearchHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ping = await _client.PingAsync(descriptor => descriptor, cancellationToken);
            var entityExists = await IndexExistsAsync(_options.EntityIndex, cancellationToken);
            var entryExists = await IndexExistsAsync(_options.EntryIndex, cancellationToken);
            var goalExists = await IndexExistsAsync(_options.GoalIndex, cancellationToken);

            return new SearchHealthResponse(
                Enabled: true,
                Endpoint: _options.Endpoint,
                Region: _options.Region,
                PingOk: ping.IsValid,
                EntityIndexExists: entityExists,
                EntryIndexExists: entryExists,
                GoalIndexExists: goalExists,
                Error: ping.IsValid ? null : ping.ServerError?.ToString() ?? "Ping failed.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenSearch health check failed.");
            return new SearchHealthResponse(
                Enabled: true,
                Endpoint: _options.Endpoint,
                Region: _options.Region,
                PingOk: false,
                EntityIndexExists: false,
                EntryIndexExists: false,
                GoalIndexExists: false,
                Error: ex.Message);
        }
    }

    public async Task<SearchBootstrapResponse> BootstrapIndicesAsync(CancellationToken cancellationToken = default)
    {
        var created = 0;
        var existing = 0;
        var failed = 0;
        var messages = new List<string>();

        var plans = new[]
        {
            new IndexPlan(_options.EntityIndex, CreateEntityIndexAsync),
            new IndexPlan(_options.EntryIndex, CreateEntryIndexAsync),
            new IndexPlan(_options.GoalIndex, CreateGoalIndexAsync)
        };

        foreach (var plan in plans)
        {
            try
            {
                var exists = await IndexExistsAsync(plan.Name, cancellationToken);
                if (exists)
                {
                    existing++;
                    messages.Add($"{plan.Name}: already exists.");
                    continue;
                }

                var ok = await plan.Create(cancellationToken);
                if (ok)
                {
                    created++;
                    messages.Add($"{plan.Name}: created.");
                }
                else
                {
                    failed++;
                    messages.Add($"{plan.Name}: failed to create.");
                }
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add($"{plan.Name}: exception {ex.Message}");
                _logger.LogWarning(ex, "Failed bootstrapping OpenSearch index {Index}.", plan.Name);
            }
        }

        return new SearchBootstrapResponse(true, created, existing, failed, messages);
    }

    private async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        var exists = await _client.Indices.ExistsAsync(indexName, ct: cancellationToken);
        return exists.Exists;
    }

    private async Task<bool> CreateEntityIndexAsync(CancellationToken cancellationToken)
    {
        var response = await _client.Indices.CreateAsync(_options.EntityIndex, descriptor => descriptor
            .Map<EntitySearchDocument>(map => map
                .AutoMap()
                .Properties(props => props
                    .Keyword(k => k.Name(x => x.UserId))
                    .Keyword(k => k.Name(x => x.Kind))
                    .Text(t => t.Name(x => x.CanonicalName))
                    .Keyword(k => k.Name(x => x.NormalizedCanonicalName))
                    .Keyword(k => k.Name(x => x.AnchorKey))
                    .Keyword(k => k.Name(x => x.Aliases))
                    .Text(t => t.Name(x => x.EntityCard))
                    .Text(t => t.Name(x => x.RelationHints))
                    .Date(d => d.Name(x => x.UpdatedAt)))), cancellationToken);

        return response.IsValid;
    }

    private async Task<bool> CreateEntryIndexAsync(CancellationToken cancellationToken)
    {
        var response = await _client.Indices.CreateAsync(_options.EntryIndex, descriptor => descriptor
            .Map<EntrySearchDocument>(map => map
                .AutoMap()
                .Properties(props => props
                    .Keyword(k => k.Name(x => x.UserId))
                    .Text(t => t.Name(x => x.Content))
                    .Date(d => d.Name(x => x.CreatedAt))
                    .Date(d => d.Name(x => x.UpdatedAt)))), cancellationToken);

        return response.IsValid;
    }

    private async Task<bool> CreateGoalIndexAsync(CancellationToken cancellationToken)
    {
        var response = await _client.Indices.CreateAsync(_options.GoalIndex, descriptor => descriptor
            .Map<GoalSearchDocument>(map => map
                .AutoMap()
                .Properties(props => props
                    .Keyword(k => k.Name(x => x.UserId))
                    .Text(t => t.Name(x => x.Title))
                    .Text(t => t.Name(x => x.Description))
                    .Keyword(k => k.Name(x => x.Status))
                    .Date(d => d.Name(x => x.CreatedAt))
                    .Date(d => d.Name(x => x.CompletedAt)))), cancellationToken);

        return response.IsValid;
    }

    private sealed record IndexPlan(string Name, Func<CancellationToken, Task<bool>> Create);

    private sealed class EntitySearchDocument
    {
        public Guid UserId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string CanonicalName { get; set; } = string.Empty;
        public string NormalizedCanonicalName { get; set; } = string.Empty;
        public string? AnchorKey { get; set; }
        public List<string> Aliases { get; set; } = new();
        public string EntityCard { get; set; } = string.Empty;
        public List<string> RelationHints { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class EntrySearchDocument
    {
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class GoalSearchDocument
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
