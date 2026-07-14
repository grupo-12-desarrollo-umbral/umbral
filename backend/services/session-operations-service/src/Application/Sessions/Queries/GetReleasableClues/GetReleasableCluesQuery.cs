using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Queries.GetReleasableClues;

[Authorize(Roles = "Operator")]
public sealed record GetReleasableCluesQuery(Guid LiveSessionId)
    : IRequest<ReleasableCluesDto>;
