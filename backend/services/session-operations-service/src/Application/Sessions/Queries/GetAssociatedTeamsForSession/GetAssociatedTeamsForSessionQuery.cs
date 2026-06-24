using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

[Authorize(Roles = "Operator")]
public sealed record GetAssociatedTeamsForSessionQuery(Guid LiveSessionId) : IRequest<SessionAssociatedTeamsDto>;
