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
    private readonly TimeProvider _timeProvider;

    public GetParticipantTeamBoardQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        IRuntimeParticipationGuard runtimeParticipationGuard,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _runtimeParticipationGuard = runtimeParticipationGuard;
        _timeProvider = timeProvider;
    }

    public async Task<ParticipantTeamBoardDto> Handle(
        GetParticipantTeamBoardQuery request,
        CancellationToken cancellationToken)
    {
        await _runtimeParticipationGuard.EnsureAllowedAsync(
            request.LiveSessionId,
            request.TeamId,
            request.Token,
            cancellationToken);

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        var snapshot = liveSession.ProjectParticipantTeamBoard(request.TeamId, _timeProvider.GetUtcNow());
        var timerDto = SessionTimerSnapshotDtoFactory.Create(liveSession, request.TeamId, snapshot.TimerSnapshot);

        return ParticipantTeamBoardDtoFactory.Create(liveSession, snapshot, timerDto);
    }
}
