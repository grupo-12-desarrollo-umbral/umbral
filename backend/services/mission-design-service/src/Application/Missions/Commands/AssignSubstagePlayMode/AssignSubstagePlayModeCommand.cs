using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;

[Authorize(Roles = Roles.Administrator)]
public sealed record AssignSubstagePlayModeCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    string PlayMode) : IRequest<MissionDto>;
