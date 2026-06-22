using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<Mission> Missions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
