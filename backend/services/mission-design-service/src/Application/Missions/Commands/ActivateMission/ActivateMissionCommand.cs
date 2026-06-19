using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.ActivateMission;

[Authorize(Roles = Roles.Administrator)]
public sealed record ActivateMissionCommand(int Id) : IRequest<MissionDto>;
