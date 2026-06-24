using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

[Authorize(Roles = "Administrator")]
public sealed record AssignOperatorToSessionCommand(
    Guid LiveSessionId,
    int OperatorUserId) : IRequest<AssignOperatorToSessionResultDto>;
