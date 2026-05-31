using umbral_backend.Application.Common.Models;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken);

    Task<PagedResult<Team>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<bool> TeamCodeExistsAsync(string teamCode, Guid? excludeTeamId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task AddAsync(Team team, CancellationToken cancellationToken);

    Task UpdateAsync(Team team, CancellationToken cancellationToken);
}
