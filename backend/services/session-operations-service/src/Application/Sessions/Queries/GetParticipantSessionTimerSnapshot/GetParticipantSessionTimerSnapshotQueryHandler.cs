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
    private readonly IParticipantMembershipAccessClient _participantMembershipAccessClient;
    private readonly TimeProvider _timeProvider;

    public GetParticipantSessionTimerSnapshotQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        IParticipantMembershipAccessClient participantMembershipAccessClient,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _participantMembershipAccessClient = participantMembershipAccessClient;
        _timeProvider = timeProvider;
    }

    public async Task<SessionTimerSnapshotDto> Handle(
        GetParticipantSessionTimerSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var accessDecision = await _participantMembershipAccessClient.ValidateAsync(
            request.LiveSessionId,
            request.TeamId,
            request.Token,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            throw new ForbiddenAccessException();
        }

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        var snapshot = liveSession.GetAuthoritativeSessionTimerSnapshot(_timeProvider.GetUtcNow());

        return SessionTimerSnapshotDtoFactory.Create(liveSession, request.TeamId, snapshot);
    }
}
