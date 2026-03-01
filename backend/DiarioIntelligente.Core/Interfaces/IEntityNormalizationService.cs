using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface IEntityNormalizationService
{
    Task<NormalizeEntitiesResponse> NormalizeUserEntitiesAsync(Guid userId, CancellationToken cancellationToken = default);
}
