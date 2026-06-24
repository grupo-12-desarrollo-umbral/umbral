using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;

[Authorize(Roles = "Participant")]
public sealed record GetParticipantSessionTimerSnapshotQuery(
    Guid LiveSessionId,
    Guid TeamId,
    string? Token) : IRequest<SessionTimerSnapshotDto>;
