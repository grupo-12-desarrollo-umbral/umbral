using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissions;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class GetMissionsQueryHandler : IRequestHandler<GetMissionsQuery, IReadOnlyList<MissionSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMissionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MissionSummaryDto>> Handle(GetMissionsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Missions
            .AsNoTracking()
            .OrderBy(mission => mission.Id)
            .Select(mission => new MissionSummaryDto(
                mission.Id,
                mission.Name,
                mission.Description,
                mission.ActivationState.ToString()))
            .ToListAsync(cancellationToken);
    }
}
