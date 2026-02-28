using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.Core.Interfaces;

public interface IPersonalModelService
{
    Task<PersonalModelResponse> BuildAsync(Guid userId, CancellationToken cancellationToken = default);
}
