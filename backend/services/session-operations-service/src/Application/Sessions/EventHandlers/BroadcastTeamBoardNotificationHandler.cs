using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Pushes each team's participant board (HU-23) on the transitions that change what the board shows:
/// a session-state change (state/timer), a substage advancement (active substage/clues), an operative
/// clue, and a resolved target (the X/n target-progress numerator). Re-loads the session read-only and
/// projects per team — never mutates state, so there is no re-entrancy loop.
/// </summary>
public sealed class BroadcastTeamBoardNotificationHandler
    : INotificationHandler<SessionStateChangedEvent>,
        INotificationHandler<SubstageAdvancedEvent>,
        INotificationHandler<OperativeClueAddedEvent>,
        INotificationHandler<TargetResolvedEvent>
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

    public Task Handle(OperativeClueAddedEvent notification, CancellationToken cancellationToken)
    {
        return BroadcastSingleTeamBoardAsync(
            notification.LiveSessionId,
            notification.TeamId,
            cancellationToken);
    }

    // A target scan bumps the team's resolved-target count, but only the scanning participant learns it
    // (they pull their own board over REST). Re-project and push the whole team's board so every member's
    // X/n numerator advances live — keyed on the session-scoped TeamId the team group and projection use.
    public Task Handle(TargetResolvedEvent notification, CancellationToken cancellationToken)
    {
        return BroadcastSingleTeamBoardAsync(
            notification.LiveSessionId,
            notification.TeamId,
            cancellationToken);
    }

    private async Task BroadcastAllTeamBoardsAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var liveSession = await LoadAsync(liveSessionId, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        foreach (var team in liveSession.Teams)
        {
            await ProjectAndBroadcastAsync(liveSession, team.TeamId, now, cancellationToken);
        }
    }

    private async Task BroadcastSingleTeamBoardAsync(
        Guid liveSessionId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var liveSession = await LoadAsync(liveSessionId, cancellationToken);
        await ProjectAndBroadcastAsync(
            liveSession,
            teamId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task<LiveSession> LoadAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);
    }

    private async Task ProjectAndBroadcastAsync(
        LiveSession liveSession,
        Guid teamId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshot = liveSession.ProjectParticipantTeamBoard(teamId, now);
        var timerDto = SessionTimerSnapshotDtoFactory.Create(liveSession, teamId, snapshot.TimerSnapshot);
        var board = ParticipantTeamBoardDtoFactory.Create(liveSession, snapshot, timerDto);

        await _teamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync(board, cancellationToken);
    }
}
