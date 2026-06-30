using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

[Authorize(Roles = "Operator")]
public sealed record AssociateTeamToSessionByCodeCommand(
    string SessionCode,
    Guid ReferenceTeamId) : IRequest<AssociateTeamToSessionResultDto>;
