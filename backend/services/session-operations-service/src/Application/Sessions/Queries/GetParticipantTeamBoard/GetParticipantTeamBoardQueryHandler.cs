using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Queries.GetParticipantTeamBoard;

public sealed class GetParticipantTeamBoardQueryHandler
    : IRequestHandler<GetParticipantTeamBoardQuery, ParticipantTeamBoardDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IRuntimeParticipationGuard _runtimeParticipationGuard;
    private readonly IParticipantSessionMembershipChecker _membershipChecker;
    private readonly TimeProvider _timeProvider;

    public GetParticipantTeamBoardQueryHandler(
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

    public async Task<ParticipantTeamBoardDto> Handle(
        GetParticipantTeamBoardQuery request,
        CancellationToken cancellationToken)
    {
        await _runtimeParticipationGuard.EnsureAllowedAsync(
            request.LiveSessionId,
            cancellationToken);

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        // NotFound (above) precedes this membership gate on purpose so a missing session stays 404.
        if (!_membershipChecker.Check(liveSession, request.TeamId).IsAllowed)
        {
            throw new ForbiddenAccessException();
        }

        var snapshot = liveSession.ProjectParticipantTeamBoard(request.TeamId, _timeProvider.GetUtcNow());
        var timerDto = SessionTimerSnapshotDtoFactory.Create(liveSession, request.TeamId, snapshot.TimerSnapshot);

        return ParticipantTeamBoardDtoFactory.Create(liveSession, snapshot, timerDto);
    }
}
