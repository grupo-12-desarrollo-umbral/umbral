using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public static class OperatorSessionPanelDtoFactory
{
    public static OperatorSessionPanelDto Create(
        LiveSession liveSession,
        OperatorSessionPanelSnapshot snapshot)
    {
        var timer = SessionTimerSnapshotDtoFactory.Create(liveSession, teamId: null, snapshot.TimerSnapshot);
        var teamProgress = snapshot.TeamProgress
            .Select(progress => new OperatorTeamProgressDto(
                progress.TeamId,
                progress.TeamCode,
                progress.DisplayName,
                progress.CurrentScore,
                progress.ActiveSubstageContext is null
                    ? null
                    : new ActiveSubstageContextDto(
                        progress.ActiveSubstageContext.SubstageSnapshotId,
                        progress.ActiveSubstageContext.PlayMode.ToString(),
                        progress.ActiveSubstageContext.Title,
                        progress.ActiveSubstageContext.TotalActiveTargets,
                        progress.ActiveSubstageContext.ResolvedTargets,
                        progress.ActiveSubstageContext.ActiveQuestionSequenceOrder,
                        progress.ActiveSubstageContext.ActiveQuestionTimeLimitSeconds)))
            .ToList();

        return new OperatorSessionPanelDto(
            snapshot.LiveSessionId,
            snapshot.State.ToString(),
            timer,
            teamProgress);
    }
}
