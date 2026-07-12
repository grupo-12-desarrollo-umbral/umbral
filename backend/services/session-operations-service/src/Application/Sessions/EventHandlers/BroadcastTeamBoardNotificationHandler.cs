using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Pushes each team's participant board (HU-23) on the transitions that change what the board shows:
/// a session-state change (state/timer) and a substage advancement (active substage/clues). Re-loads
/// the session read-only and projects per team — never mutates state, so there is no re-entrancy loop.
/// </summary>
public sealed class BroadcastTeamBoardNotificationHandler
    : INotificationHandler<SessionStateChangedEvent>,
        INotificationHandler<SubstageAdvancedEvent>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ITeamBoardBroadcaster _teamBoardBroadcaster;
    private readonly TimeProvider _timeProvider;

    public BroadcastTeamBoardNotificationHandler(
        ILiveSessionRepository liveSessionRepository,
        ITeamBoardBroadcaster teamBoardBroadcaster,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _teamBoardBroadcaster = teamBoardBroadcaster;
        _timeProvider = timeProvider;
    }

    public Task Handle(SessionStateChangedEvent notification, CancellationToken cancellationToken)
    {
        return BroadcastAllTeamBoardsAsync(notification.LiveSessionId, cancellationToken);
    }

    public Task Handle(SubstageAdvancedEvent notification, CancellationToken cancellationToken)
    {
        return BroadcastAllTeamBoardsAsync(notification.LiveSessionId, cancellationToken);
    }

    private async Task BroadcastAllTeamBoardsAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);

        var now = _timeProvider.GetUtcNow();

        foreach (var team in liveSession.Teams)
        {
            var snapshot = liveSession.ProjectParticipantTeamBoard(team.TeamId, now);
            var timerDto = SessionTimerSnapshotDtoFactory.Create(liveSession, team.TeamId, snapshot.TimerSnapshot);
            var board = ParticipantTeamBoardDtoFactory.Create(liveSession, snapshot, timerDto);

            await _teamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync(board, cancellationToken);
        }
    }
}
