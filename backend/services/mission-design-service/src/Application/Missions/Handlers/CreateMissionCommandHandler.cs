using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IApplicationDbContext _context;

    public CreateMissionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = Mission.Create(
            request.Name,
            request.Description,
            request.Difficulty,
            request.MaximumTimeMinutes);

        _context.Missions.Add(mission);
        await _context.SaveChangesAsync(cancellationToken);

        return new MissionDto(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.Difficulty.Value,
            mission.MaximumTime.Minutes,
            mission.ActivationState.ToString());
    }
}
