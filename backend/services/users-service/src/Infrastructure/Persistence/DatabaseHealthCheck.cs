using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Persistence;

public sealed class DatabaseHealthCheck(ApplicationDbContext dbContext) : IDatabaseHealthCheck
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        => dbContext.Database.CanConnectAsync(cancellationToken);
}
