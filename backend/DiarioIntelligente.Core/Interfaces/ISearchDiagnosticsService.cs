using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface ISearchDiagnosticsService
{
    Task<SearchHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<SearchBootstrapResponse> BootstrapIndicesAsync(CancellationToken cancellationToken = default);
}
