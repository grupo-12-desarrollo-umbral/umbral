using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Common;

/// <summary>
/// Cross-aggregate readiness check that the pure structural
/// <see cref="Domain.Services.MissionActivationPolicy"/> cannot perform: it verifies the trivia
/// question timers a mission schedules can even fit inside the mission's single maximum-time budget.
/// <para>
/// A mission runs one shared countdown (<c>MaximumTime</c>) covering every substage — including an
/// open-ended treasure hunt whose duration is unknowable at authoring time. This is therefore a
/// <b>necessary, not sufficient</b> condition: it only proves a mission is misconfigured when the
/// trivia timers <i>alone</i> already exceed the whole budget (the mission clock would cut the trivia
/// off mid-question). Passing it never asserts the mission as a whole "fits". Shared between readiness
/// evaluation and activation so the two cannot drift.
/// </para>
/// </summary>
internal static class MissionTriviaTimeBudgetChecker
{
    /// <summary>
    /// One failure when the summed trivia question timers exceed the mission's maximum-time budget.
    /// A quiz selected by more than one substage plays each time, so timers are summed per substage,
    /// not per distinct quiz. A quiz id absent from <paramref name="timerSecondsByQuizId"/> (deleted)
    /// contributes no time — the publication check surfaces that separately.
    /// </summary>
    public static IReadOnlyList<string> Evaluate(
        Mission mission,
        IReadOnlyDictionary<int, int> timerSecondsByQuizId)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(timerSecondsByQuizId);

        var totalTriviaSeconds = 0;
        foreach (var substage in mission.Stages.SelectMany(stage => stage.Substages))
        {
            if (substage.PlayMode != SubstagePlayMode.Trivia || substage.TriviaQuizId is null)
            {
                continue;
            }

            if (timerSecondsByQuizId.TryGetValue(substage.TriviaQuizId.Value, out var quizSeconds))
            {
                totalTriviaSeconds += quizSeconds;
            }
        }

        var budgetSeconds = mission.MaximumTime.Minutes * 60;
        if (totalTriviaSeconds <= budgetSeconds)
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            $"Trivia question timers total {totalTriviaSeconds}s, which exceeds the mission maximum time " +
            $"of {mission.MaximumTime.Minutes} minutes ({budgetSeconds}s). The mission clock will cut the " +
            "trivia off. Shorten the question timers or increase the maximum time.",
        };
    }
}
