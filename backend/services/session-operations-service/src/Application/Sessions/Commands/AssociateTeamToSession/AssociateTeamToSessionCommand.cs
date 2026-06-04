using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

[Authorize(Roles = "Operator")]
public sealed record AssociateTeamToSessionCommand(
    Guid LiveSessionId,
    Guid ReferenceTeamId) : IRequest<AssociateTeamToSessionResultDto>;
