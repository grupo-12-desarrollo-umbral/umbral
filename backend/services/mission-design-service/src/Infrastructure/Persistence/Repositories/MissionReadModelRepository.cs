using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class MissionReadModelRepository : IMissionReadModelRepository
{
    private readonly ApplicationDbContext _context;

    public MissionReadModelRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MissionSummaryDto>> GetMissionCatalogAsync(CancellationToken cancellationToken)
    {
        return await _context.Missions
            .AsNoTracking()
            .OrderByDescending(mission => mission.LastModified)
            .Select(mission => new MissionSummaryDto(
                mission.Id,
                mission.Name,
                mission.Description,
                mission.Difficulty.Value,
                mission.ActivationState.ToString()))
            .ToListAsync(cancellationToken);
    }

    public Task<MissionDto?> GetMissionDetailAsync(int missionId, CancellationToken cancellationToken)
    {
        return _context.Missions
            .AsNoTracking()
            .Where(mission => mission.Id == missionId)
            .Select(mission => new MissionDto(
                mission.Id,
                mission.Name,
                mission.Description,
                mission.Difficulty.Value,
                mission.MaximumTime.Minutes,
                mission.ActivationState.ToString()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
