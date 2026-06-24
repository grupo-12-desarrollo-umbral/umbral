using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.RemoveMissionNode;

[Authorize(Roles = Roles.Administrator)]
public sealed record RemoveMissionNodeCommand(int MissionId, int NodeId) : IRequest<MissionDto>;
