using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSessionReference;

[Authorize(Roles = "Administrator,Operator")]
public sealed record AssociateTeamToSessionReferenceCommand(
    Guid LiveSessionId,
    string SessionCode,
    Guid TeamId) : IRequest;
