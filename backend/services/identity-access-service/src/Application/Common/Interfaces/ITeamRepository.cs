using umbral_backend.Application.Common.Models;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITeamRepository
{
    Task<RegisteredTeam?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken);

    Task<RegisteredTeam?> GetByIdWithMembershipsAsync(Guid teamId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task<PagedResult<RegisteredTeam>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<bool> TeamCodeExistsAsync(string teamCode, Guid? excludeTeamId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task AddAsync(RegisteredTeam team, CancellationToken cancellationToken);

    Task UpdateAsync(RegisteredTeam team, CancellationToken cancellationToken);
}
