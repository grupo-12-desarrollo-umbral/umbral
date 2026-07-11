using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public static class ParticipantTeamBoardDtoFactory
{
    public static ParticipantTeamBoardDto Create(
        LiveSession liveSession,
        ParticipantTeamBoardSnapshot snapshot,
        SessionTimerSnapshotDto timerDto)
    {
        var activeSubstage = snapshot.ActiveSubstageContext is not null
            ? MapActiveSubstage(snapshot.ActiveSubstageContext)
            : null;

        var visibleClues = snapshot.VisibleClues
            .Select(clue => new VisibleClueDto(
                clue.TargetSnapshotId,
                clue.ClueText,
                clue.TargetName))
            .ToList();

        return new ParticipantTeamBoardDto(
            liveSession.LiveSessionId,
            snapshot.TeamId,
            snapshot.TeamDisplayName,
            snapshot.TeamCode,
            snapshot.CurrentScore,
            timerDto,
            activeSubstage,
            visibleClues);
    }

    private static ActiveSubstageContextDto MapActiveSubstage(ActiveSubstageContext context)
    {
        return new ActiveSubstageContextDto(
            context.SubstageSnapshotId,
            context.PlayMode.ToString(),
            context.Title,
            context.TotalActiveTargets,
            context.ResolvedTargets,
            context.ActiveQuestionSequenceOrder,
            context.ActiveQuestionTimeLimitSeconds);
    }
}
