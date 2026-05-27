using Microsoft.EntityFrameworkCore;

namespace umbral_backend.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
