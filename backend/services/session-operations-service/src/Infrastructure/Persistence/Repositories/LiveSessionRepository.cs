using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class LiveSessionRepository : ILiveSessionRepository
{
    private readonly ApplicationDbContext _context;

    public LiveSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<LiveSession?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return _context.LiveSessions
            .Include(session => session.Teams)
                .ThenInclude(team => team.Members)
            .Include(session => session.Participants)
            .Include(session => session.JoinContexts)
            .AsSplitQuery()
            .SingleOrDefaultAsync(session => session.LiveSessionId == liveSessionId, cancellationToken);
    }

    public async Task UpdateAsync(LiveSession liveSession, CancellationToken cancellationToken)
    {
        if (_context.Entry(liveSession).State == EntityState.Detached)
        {
            _context.LiveSessions.Update(liveSession);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
