using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

public sealed class BroadcastClueReleasedNotificationHandler
    : INotificationHandler<ClueReleasedEvent>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ITeamBoardBroadcaster _teamBoardBroadcaster;
    private readonly TimeProvider _timeProvider;

    public BroadcastClueReleasedNotificationHandler(
        ILiveSessionRepository liveSessionRepository,
        ITeamBoardBroadcaster teamBoardBroadcaster,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _teamBoardBroadcaster = teamBoardBroadcaster;
        _timeProvider = timeProvider;
    }

    public async Task Handle(ClueReleasedEvent notification, CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(notification.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), notification.LiveSessionId);

        var now = _timeProvider.GetUtcNow();
        var snapshot = liveSession.ProjectParticipantTeamBoard(notification.TeamId, now);
        var timerDto = SessionTimerSnapshotDtoFactory.Create(liveSession, notification.TeamId, snapshot.TimerSnapshot);
        var board = ParticipantTeamBoardDtoFactory.Create(liveSession, snapshot, timerDto);

        await _teamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync(board, cancellationToken);
    }
}
