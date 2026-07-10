using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.UpdateTarget;

[Authorize(Roles = Roles.Administrator)]
public sealed record UpdateTargetCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    int TargetId,
    string Name,
    string QrCode,
    int SequenceOrder,
    bool IsActive,
    int? Score = null) : IRequest<MissionDto>;
