using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

[Authorize(Roles = "Administrator")]
public sealed record ListAssignableSessionsQuery()
    : IRequest<IReadOnlyList<SessionOperatorSummaryDto>>;
