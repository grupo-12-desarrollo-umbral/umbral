using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Persistence;

// Keeps the change tracker out of the Application layer, which has no EF dependency by design.
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ScoringMonitoringDbContext _context;

    public UnitOfWork(ScoringMonitoringDbContext context)
    {
        _context = context;
    }

    public void ResetTracking() => _context.ChangeTracker.Clear();
}
