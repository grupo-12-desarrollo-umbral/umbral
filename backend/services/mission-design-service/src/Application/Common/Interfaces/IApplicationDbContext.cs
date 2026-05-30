using Microsoft.EntityFrameworkCore;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Mission> Missions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
