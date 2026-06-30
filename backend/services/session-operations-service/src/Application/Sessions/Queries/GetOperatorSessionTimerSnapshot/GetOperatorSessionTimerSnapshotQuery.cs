using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;

[Authorize(Roles = "Operator")]
public sealed record GetOperatorSessionTimerSnapshotQuery(Guid LiveSessionId)
    : IRequest<SessionTimerSnapshotDto>;
