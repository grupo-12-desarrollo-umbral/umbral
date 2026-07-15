using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;

public sealed class GetParticipantSessionTimerSnapshotQueryHandler
    : IRequestHandler<GetParticipantSessionTimerSnapshotQuery, SessionTimerSnapshotDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IRuntimeParticipationGuard _runtimeParticipationGuard;
    private readonly IParticipantSessionMembershipChecker _membershipChecker;
    private readonly TimeProvider _timeProvider;

    public GetParticipantSessionTimerSnapshotQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        IRuntimeParticipationGuard runtimeParticipationGuard,
        IParticipantSessionMembershipChecker membershipChecker,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _runtimeParticipationGuard = runtimeParticipationGuard;
        _membershipChecker = membershipChecker;
        _timeProvider = timeProvider;
    }

    public async Task<SessionTimerSnapshotDto> Handle(
        GetParticipantSessionTimerSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        await _runtimeParticipationGuard.EnsureAllowedAsync(
            request.LiveSessionId,
            request.TeamId,
            request.Token,
            cancellationToken);

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        // NotFound (above) precedes this membership gate on purpose so a missing session stays 404.
        if (!_membershipChecker.Check(liveSession, request.TeamId).IsAllowed)
        {
            throw new ForbiddenAccessException();
        }

        var snapshot = liveSession.GetAuthoritativeSessionTimerSnapshot(_timeProvider.GetUtcNow());

        return SessionTimerSnapshotDtoFactory.Create(liveSession, request.TeamId, snapshot);
    }
}
