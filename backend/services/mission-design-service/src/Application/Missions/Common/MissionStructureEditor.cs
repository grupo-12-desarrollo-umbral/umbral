using FluentValidation.Results;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using ApplicationNotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ApplicationValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.Missions.Common;

internal static class MissionStructureEditor
{
    public static Stage FindStage(Mission mission, int stageId)
    {
        return mission.Stages.SingleOrDefault(stage => stage.Id == stageId)
            ?? throw new ApplicationNotFoundException("MissionNode", stageId);
    }

    public static Substage FindSubstage(Mission mission, int stageId, int substageId)
    {
        var stage = FindStage(mission, stageId);

        return stage.Substages.SingleOrDefault(substage => substage.Id == substageId)
            ?? throw new ApplicationNotFoundException("MissionNode", substageId);
    }

    public static Clue FindClue(Mission mission, int stageId, int substageId, int clueId)
    {
        var substage = FindSubstage(mission, stageId, substageId);

        return substage.Clues.SingleOrDefault(clue => clue.Id == clueId)
            ?? throw new ApplicationNotFoundException("MissionNode", clueId);
    }

    public static Target FindTarget(Mission mission, int stageId, int substageId, int targetId)
    {
        var substage = FindSubstage(mission, stageId, substageId);

        return substage.Targets.SingleOrDefault(target => target.Id == targetId)
            ?? throw new ApplicationNotFoundException("Target", targetId);
    }

    public static void RenameNode(
        Mission mission,
        int nodeId,
        string title,
        int sequenceOrder,
        string? text,
        ClueVisibilityPolicy? visibility)
    {
        if (mission.Stages.SingleOrDefault(stage => stage.Id == nodeId) is { } stage)
        {
            mission.RenameNode(stage.Id, title, sequenceOrder);
            return;
        }

        foreach (var candidateStage in mission.Stages)
        {
            if (candidateStage.Substages.SingleOrDefault(substage => substage.Id == nodeId) is { } substage)
            {
                substage.Rename(title, sequenceOrder);
                RefreshReadiness(mission);
                return;
            }

            foreach (var candidateSubstage in candidateStage.Substages)
            {
                if (candidateSubstage.Clues.SingleOrDefault(clue => clue.Id == nodeId) is { } clue)
                {
                    var replacement = Clue.Create(
                        title,
                        sequenceOrder,
                        text ?? clue.Text,
                        visibility ?? clue.Visibility);
                    replacement.Id = clue.Id;
                    candidateSubstage.RemoveChild(clue);
                    candidateSubstage.AddClue(replacement);
                    RefreshReadiness(mission);
                    return;
                }
            }
        }

        throw new ApplicationNotFoundException("MissionNode", nodeId);
    }

    public static void RemoveNode(Mission mission, int nodeId)
    {
        if (mission.Stages.Any(stage => stage.Id == nodeId))
        {
            mission.RemoveStage(nodeId);
            return;
        }

        foreach (var stage in mission.Stages)
        {
            if (stage.Substages.SingleOrDefault(substage => substage.Id == nodeId) is { } substage)
            {
                stage.RemoveChild(substage);
                RefreshReadiness(mission);
                return;
            }

            foreach (var candidateSubstage in stage.Substages)
            {
                if (candidateSubstage.Clues.SingleOrDefault(clue => clue.Id == nodeId) is { } clue)
                {
                    if (candidateSubstage.Targets.Any(target => target.ClueId == clue.Id))
                    {
                        throw ValidationFailure(
                            "ClueId",
                            "A clue associated with a target must be unassociated before it can be removed.");
                    }

                    candidateSubstage.RemoveChild(clue);
                    RefreshReadiness(mission);
                    return;
                }
            }
        }

        throw new ApplicationNotFoundException("MissionNode", nodeId);
    }

    public static void AssignPlayMode(
        Mission mission,
        int stageId,
        int substageId,
        SubstagePlayMode playMode)
    {
        var stage = FindStage(mission, stageId);
        var existing = stage.Substages.SingleOrDefault(substage => substage.Id == substageId)
            ?? throw new ApplicationNotFoundException("MissionNode", substageId);

        if (existing.PlayMode == playMode)
        {
            return;
        }

        var replacement = playMode == SubstagePlayMode.TreasureHunt
            ? Substage.CreateTreasureHunt(existing.Title, existing.SequenceOrder)
            : Substage.CreateTrivia(existing.Title, existing.SequenceOrder);
        replacement.Id = existing.Id;

        foreach (var clue in existing.Clues)
        {
            replacement.AddClue(clue);
        }

        stage.RemoveChild(existing);
        stage.AddSubstage(replacement);
        RefreshReadiness(mission);
    }

    public static void UnassociateClueFromTarget(Mission mission, int stageId, int substageId, int targetId)
    {
        var substage = FindSubstage(mission, stageId, substageId);
        var existing = substage.Targets.SingleOrDefault(target => target.Id == targetId)
            ?? throw new ApplicationNotFoundException("Target", targetId);

        if (existing.ClueId is null)
        {
            return;
        }

        substage.RemoveTarget(existing.Id);
        var replacement = substage.AddTarget(
            existing.Name,
            existing.QrCode,
            existing.SequenceOrder,
            existing.IsActive);
        replacement.Id = existing.Id;
        RefreshReadiness(mission);
    }

    public static void RefreshReadiness(Mission mission)
    {
        mission.UpdateDetails(
            mission.Name,
            mission.Description,
            mission.Difficulty.Value,
            mission.MaximumTime.Minutes);
    }

    public static ApplicationValidationException ValidationFailure(string propertyName, string message)
    {
        return new ApplicationValidationException([new ValidationFailure(propertyName, message)]);
    }
}
