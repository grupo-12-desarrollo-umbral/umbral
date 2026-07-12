using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.ValueObjects;

public sealed class OperatorSessionPanelSnapshot : ValueObject
{
    private OperatorSessionPanelSnapshot(
        Guid liveSessionId,
        SessionState state,
        AuthoritativeSessionTimerSnapshot timerSnapshot,
        IReadOnlyList<OperatorTeamProgress> teamProgress)
    {
        LiveSessionId = liveSessionId;
        State = state;
        TimerSnapshot = timerSnapshot;
        TeamProgress = teamProgress;
    }

    public Guid LiveSessionId { get; }

    public SessionState State { get; }

    public AuthoritativeSessionTimerSnapshot TimerSnapshot { get; }

    public IReadOnlyList<OperatorTeamProgress> TeamProgress { get; }

    public static OperatorSessionPanelSnapshot Create(
        Guid liveSessionId,
        SessionState state,
        AuthoritativeSessionTimerSnapshot timerSnapshot,
        IReadOnlyList<OperatorTeamProgress> teamProgress)
    {
        return new OperatorSessionPanelSnapshot(liveSessionId, state, timerSnapshot, teamProgress);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LiveSessionId;
        yield return State;
        yield return TimerSnapshot;

        foreach (var progress in TeamProgress)
        {
            yield return progress;
        }
    }
}
