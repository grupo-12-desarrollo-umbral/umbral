using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class MissionRepository : IMissionRepository
{
    private readonly ApplicationDbContext _context;

    public MissionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Mission?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
    {
        return _context.Missions
            .SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);
    }

    public async Task AddAsync(Mission mission, CancellationToken cancellationToken)
    {
        await _context.Missions.AddAsync(mission, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Mission mission, CancellationToken cancellationToken)
    {
        _context.Missions.Update(mission);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
