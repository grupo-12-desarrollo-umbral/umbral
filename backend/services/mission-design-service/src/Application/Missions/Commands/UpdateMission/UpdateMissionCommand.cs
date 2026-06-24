using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.UpdateMission;

[Authorize(Roles = Roles.Administrator)]
public sealed record UpdateMissionCommand(
    int Id,
    string Name,
    string Description,
    string Difficulty,
    int MaximumTimeMinutes) : IRequest<MissionDto>;
