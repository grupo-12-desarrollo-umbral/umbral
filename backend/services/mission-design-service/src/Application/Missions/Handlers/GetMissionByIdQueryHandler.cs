using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissionById;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class GetMissionByIdQueryHandler : IRequestHandler<GetMissionByIdQuery, MissionDto>
{
    private readonly IApplicationDbContext _context;

    public GetMissionByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MissionDto> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        var mission = await _context.Missions
            .AsNoTracking()
            .SingleOrDefaultAsync(mission => mission.Id == request.Id, cancellationToken);

        if (mission is null)
        {
            throw new NotFoundException(nameof(Mission), request.Id);
        }

        return new MissionDto(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.Difficulty.Value,
            mission.MaximumTime.Minutes,
            mission.ActivationState.ToString());
    }
}
