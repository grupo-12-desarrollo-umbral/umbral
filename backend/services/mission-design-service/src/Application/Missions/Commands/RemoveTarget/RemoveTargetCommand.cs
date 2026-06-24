using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.RemoveTarget;

[Authorize(Roles = Roles.Administrator)]
public sealed record RemoveTargetCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    int TargetId) : IRequest<MissionDto>;
