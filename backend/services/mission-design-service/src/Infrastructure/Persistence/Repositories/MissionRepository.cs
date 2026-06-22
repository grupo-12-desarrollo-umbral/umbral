using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

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
            .AsSplitQuery()
            .SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveMissionReference>> GetActiveMissionsReferencingTriviaQuizAsync(
        int triviaQuizId,
        CancellationToken cancellationToken)
    {
        // Owned stage/substage collections are part of the mission aggregate and load with it,
        // so the Ready missions are filtered in SQL and the trivia-substage match is applied in
        // memory over the loaded graph. "Active" is the Ready activation state — the only state
        // from which a session can be created against the mission.
        var readyMissions = await _context.Missions
            .AsSplitQuery()
            .Where(mission => mission.ActivationState == MissionActivation.Ready)
            .ToListAsync(cancellationToken);

        return readyMissions
            .Where(mission => mission.Stages
                .SelectMany(stage => stage.Substages)
                .Any(substage => substage.TriviaQuizId == triviaQuizId))
            .Select(mission => new ActiveMissionReference(mission.Id, mission.Name))
            .ToList();
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
