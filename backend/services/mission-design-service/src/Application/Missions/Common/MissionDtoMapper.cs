using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Missions.Common;

public static class MissionDtoMapper
{
    public static MissionDto Map(Mission mission)
    {
        return new MissionDto(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.Difficulty.Value,
            mission.MaximumTime.Minutes,
            mission.ActivationState.ToString(),
            mission.Stages.Select(MapStage).ToList());
    }

    private static MissionStageDto MapStage(Stage stage)
    {
        return new MissionStageDto(
            stage.Id,
            stage.Title,
            stage.SequenceOrder,
            stage.Substages
                .OrderBy(substage => substage.SequenceOrder)
                .Select(MapSubstage)
                .ToList());
    }

    private static MissionSubstageDto MapSubstage(Substage substage)
    {
        return new MissionSubstageDto(
            substage.Id,
            substage.Title,
            substage.SequenceOrder,
            substage.PlayMode.ToString(),
            substage.WinnerScore?.Points,
            substage.TriviaQuizId is null
                ? null
                : new TriviaQuestionSelectionDto(substage.TriviaQuizId.Value),
            substage.Targets.Select(MapTarget).ToList(),
            substage.Clues
                .OrderBy(clue => clue.SequenceOrder)
                .Select(MapClue)
                .ToList());
    }

    private static MissionTargetDto MapTarget(Target target)
    {
        return new MissionTargetDto(
            target.Id,
            target.Name,
            target.QrCode,
            target.SequenceOrder,
            target.IsActive,
            target.ClueId);
    }

    private static MissionClueDto MapClue(Clue clue)
    {
        return new MissionClueDto(
            clue.Id,
            clue.Title,
            clue.SequenceOrder,
            clue.Text,
            clue.Visibility.ToString());
    }
}
