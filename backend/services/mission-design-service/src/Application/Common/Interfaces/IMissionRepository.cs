using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IMissionRepository
{
    Task<Mission?> GetByIdAsync(int missionId, CancellationToken cancellationToken);

    Task AddAsync(Mission mission, CancellationToken cancellationToken);

    Task UpdateAsync(Mission mission, CancellationToken cancellationToken);
}
