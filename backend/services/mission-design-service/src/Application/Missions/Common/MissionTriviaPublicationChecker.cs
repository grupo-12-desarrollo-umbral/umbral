using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Common;

/// <summary>
/// Cross-aggregate readiness check that the pure structural
/// <see cref="Domain.Services.MissionActivationPolicy"/> cannot perform: it verifies
/// that every trivia substage still references a <see cref="TriviaQuizStatus.Published"/>
/// quiz. A quiz published at selection time can later be archived, which must demote the
/// mission from ready. Shared between readiness evaluation and activation so the two
/// cannot drift.
/// </summary>
internal static class MissionTriviaPublicationChecker
{
    /// <summary>Distinct quiz ids referenced by the mission's trivia substages.</summary>
    public static IReadOnlyCollection<int> CollectTriviaQuizIds(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return mission.Stages
            .SelectMany(stage => stage.Substages)
            .Where(substage => substage.PlayMode == SubstagePlayMode.Trivia && substage.TriviaQuizId is not null)
            .Select(substage => substage.TriviaQuizId!.Value)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// One failure per trivia substage whose referenced quiz is missing from
    /// <paramref name="statuses"/> (deleted) or whose status is not published. Substages
    /// with no selection are left to the structural policy.
    /// </summary>
    public static IReadOnlyList<string> Evaluate(
        Mission mission,
        IReadOnlyDictionary<int, TriviaQuizStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(statuses);

        var failures = new List<string>();

        foreach (var stage in mission.Stages)
        {
            foreach (var substage in stage.Substages)
            {
                if (substage.PlayMode != SubstagePlayMode.Trivia || substage.TriviaQuizId is null)
                {
                    continue;
                }

                if (!statuses.TryGetValue(substage.TriviaQuizId.Value, out var status)
                    || status != TriviaQuizStatus.Published)
                {
                    failures.Add(
                        $"Trivia substage '{substage.Title}' in stage '{stage.Title}' must select a published trivia quiz.");
                }
            }
        }

        return failures;
    }
}
