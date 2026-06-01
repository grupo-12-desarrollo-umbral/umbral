using umbral_backend.Application.Common.Security;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.DeactivateMission;

[Authorize(Roles = Roles.Administrator)]
public sealed record DeactivateMissionCommand(int Id) : IRequest;
