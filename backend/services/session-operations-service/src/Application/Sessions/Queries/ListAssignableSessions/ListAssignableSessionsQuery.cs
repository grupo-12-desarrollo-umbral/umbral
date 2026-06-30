using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

[Authorize(Roles = "Administrator,Operator")]
public sealed record ListAssignableSessionsQuery()
    : IRequest<IReadOnlyList<SessionOperatorSummaryDto>>;
