using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class GetOperatorSessionTimerSnapshotQueryHandler
    : IRequestHandler<GetOperatorSessionTimerSnapshotQuery, SessionTimerSnapshotDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;
    private readonly TimeProvider _timeProvider;

    public GetOperatorSessionTimerSnapshotQueryHandler(
        ISessionAdministrationAccessResolver accessResolver,
        TimeProvider timeProvider)
    {
        _accessResolver = accessResolver;
        _timeProvider = timeProvider;
    }

    public async Task<SessionTimerSnapshotDto> Handle(
        GetOperatorSessionTimerSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _accessResolver.GetAuthorizedTimerSessionAsync(
            request.LiveSessionId, cancellationToken);

        var snapshot = liveSession.GetAuthoritativeSessionTimerSnapshot(
            _timeProvider.GetUtcNow());

        return SessionTimerSnapshotDtoFactory.Create(liveSession, teamId: null, snapshot);
    }
}
