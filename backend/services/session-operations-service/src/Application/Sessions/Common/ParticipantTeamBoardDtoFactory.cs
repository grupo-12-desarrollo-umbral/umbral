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

        var substages = snapshot.Substages
            .Select(substage => new SubstageProgressDto(
                substage.SubstageSnapshotId,
                substage.Title,
                substage.SequenceOrder,
                substage.PlayMode.ToString(),
                substage.Status.ToString()))
            .ToList();

        var visibleClues = snapshot.VisibleClues
            .Select(clue => new VisibleClueDto(
                clue.TargetSnapshotId,
                clue.ClueText,
                clue.TargetName))
            .ToList();

        var activeTargets = snapshot.ActiveTargets
            .Select(target => new ActiveTargetDto(
                target.TargetSnapshotId,
                target.Name,
                target.SequenceOrder,
                target.Latitude,
                target.Longitude))
            .ToList();

        return new ParticipantTeamBoardDto(
            liveSession.LiveSessionId,
            snapshot.TeamId,
            snapshot.TeamDisplayName,
            snapshot.TeamCode,
            snapshot.CurrentScore,
            timerDto,
            activeSubstage,
            substages,
            visibleClues,
            activeTargets);
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
            context.ActiveQuestionTimeLimitSeconds,
            context.Targets
                .Select(target => new ActiveSubstageTargetDto(
                    target.TargetSnapshotId,
                    target.Name,
                    target.SequenceOrder,
                    target.HasHiddenClue))
                .ToList());
    }
}
