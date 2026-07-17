using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Source-readiness decision over a mission's authored runtime plan. This is a
/// <c>MissionDesign</c> authoring decision and is intentionally distinct from the
/// runtime <c>LiveSession</c> lifecycle owned by <c>SessionOperations</c>.
/// <para>
/// A mission is runtime-ready when every stage has at least one substage, every
/// substage declares exactly one play mode, every <c>TreasureHunt</c> substage has
/// at least one active target, and every <c>Trivia</c> substage
/// selects a published trivia quiz.
/// </para>
/// </summary>
public static class MissionActivationPolicy
{
    public static MissionActivation DetermineActivationState(bool isActive, bool satisfiesStructureRequirements)
    {
        if (!isActive)
        {
            return MissionActivation.Inactive;
        }

        if (!satisfiesStructureRequirements)
        {
            return MissionActivation.Draft;
        }

        return MissionActivation.Ready;
    }

    public static bool SatisfiesRuntimePlan(Mission mission)
    {
        return EvaluateReadiness(mission).Count == 0;
    }

    public static IReadOnlyList<string> EvaluateReadiness(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var failures = new List<string>();

        if (mission.Stages.Count == 0)
        {
            failures.Add("Mission must contain at least one stage.");
            return failures;
        }

        foreach (var stage in mission.Stages)
        {
            EvaluateStage(stage, failures);
        }

        EvaluateTargetQrUniqueness(mission, failures);

        return failures;
    }

    // Mission-scoped, because SessionOperations resolves a scan against the whole mission snapshot:
    // two targets sharing a code anywhere in the mission make that scan ambiguous mid-game. Authoring
    // now rejects duplicates up front, so this only catches missions authored before that guard.
    private static void EvaluateTargetQrUniqueness(Mission mission, List<string> failures)
    {
        var duplicates = mission.Stages
            .SelectMany(stage => stage.Substages)
            .SelectMany(substage => substage.Targets)
            .GroupBy(target => target.QrCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(qrCode => qrCode, StringComparer.OrdinalIgnoreCase);

        foreach (var qrCode in duplicates)
        {
            failures.Add($"QR code '{qrCode}' is shared by more than one target; target QR codes must be unique within a mission.");
        }
    }

    private static void EvaluateStage(Stage stage, List<string> failures)
    {
        var substages = stage.Substages.ToList();

        if (substages.Count == 0)
        {
            failures.Add($"Stage '{stage.Title}' must contain at least one substage.");
            return;
        }

        foreach (var substage in substages)
        {
            EvaluateSubstage(stage, substage, failures);
        }
    }

    private static void EvaluateSubstage(Stage stage, Substage substage, List<string> failures)
    {
        if (substage.PlayMode == SubstagePlayMode.TreasureHunt)
        {
            EvaluateTreasureHunt(stage, substage, failures);
            return;
        }

        EvaluateTrivia(stage, substage, failures);
    }

    private static void EvaluateTreasureHunt(Stage stage, Substage substage, List<string> failures)
    {
        if (!substage.Targets.Any(target => target.IsActive))
        {
            failures.Add(
                $"Treasure-hunt substage '{substage.Title}' in stage '{stage.Title}' must have at least one active target.");
        }
    }

    private static void EvaluateTrivia(Stage stage, Substage substage, List<string> failures)
    {
        if (substage.TriviaQuizId is null)
        {
            failures.Add(
                $"Trivia substage '{substage.Title}' in stage '{stage.Title}' must select a published trivia quiz.");
        }
    }
}
