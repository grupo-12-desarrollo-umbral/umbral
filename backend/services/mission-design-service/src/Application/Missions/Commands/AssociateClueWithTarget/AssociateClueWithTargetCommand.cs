using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;

[Authorize(Roles = Roles.Administrator)]
public sealed record AssociateClueWithTargetCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    int TargetId,
    int ClueId) : IRequest<MissionDto>;
