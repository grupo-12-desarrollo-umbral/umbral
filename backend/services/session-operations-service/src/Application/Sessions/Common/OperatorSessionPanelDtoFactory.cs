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
                progress.ReferenceTeamId,
                progress.TeamCode,
                progress.DisplayName,
                progress.CurrentScore,
                progress.ReleasedClueCount,
                progress.ActiveSubstageContext is null
                    ? null
                    : new ActiveSubstageContextDto(
                        progress.ActiveSubstageContext.SubstageSnapshotId,
                        progress.ActiveSubstageContext.PlayMode.ToString(),
                        progress.ActiveSubstageContext.Title,
                        progress.ActiveSubstageContext.TotalActiveTargets,
                        progress.ActiveSubstageContext.ResolvedTargets,
                        progress.ActiveSubstageContext.ActiveQuestionSequenceOrder,
                        progress.ActiveSubstageContext.ActiveQuestionTimeLimitSeconds,
                        progress.ActiveSubstageContext.Targets
                            .Select(target => new ActiveSubstageTargetDto(
                                target.TargetSnapshotId,
                                target.Name,
                                target.SequenceOrder,
                                target.HasHiddenClue))
                            .ToList())))
            .ToList();

        return new OperatorSessionPanelDto(
            snapshot.LiveSessionId,
            liveSession.MissionRuntimeSnapshot.MissionTitle,
            snapshot.State.ToString(),
            timer,
            teamProgress);
    }
}
