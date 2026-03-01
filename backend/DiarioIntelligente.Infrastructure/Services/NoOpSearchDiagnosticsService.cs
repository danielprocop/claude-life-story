using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class NoOpSearchDiagnosticsService : ISearchDiagnosticsService
{
    public Task<SearchHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new SearchHealthResponse(
            Enabled: false,
            Endpoint: string.Empty,
            Region: string.Empty,
            PingOk: false,
            EntityIndexExists: false,
            EntryIndexExists: false,
            GoalIndexExists: false,
            Error: "Search backend disabled."));

    public Task<SearchBootstrapResponse> BootstrapIndicesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new SearchBootstrapResponse(
            Enabled: false,
            CreatedIndices: 0,
            ExistingIndices: 0,
            FailedIndices: 0,
            Messages: new List<string> { "Search backend disabled." }));
}
