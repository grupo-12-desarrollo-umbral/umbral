using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

[Authorize(Roles = "Operator")]
public sealed record GetAssociatedTeamsForSessionByCodeQuery(string SessionCode)
    : IRequest<SessionAssociatedTeamsDto>;
