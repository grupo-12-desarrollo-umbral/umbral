using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.AddTarget;

[Authorize(Roles = Roles.Administrator)]
public sealed record AddTargetCommand(
    int MissionId,
    int StageId,
    int SubstageId,
    string Name,
    string QrCode,
    int SequenceOrder,
    double Latitude,
    double Longitude,
    bool IsActive = true) : IRequest<MissionDto>;
