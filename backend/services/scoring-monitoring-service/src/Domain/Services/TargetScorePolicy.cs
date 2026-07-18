using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Treasure-hunt score-calculation strategy — this is where the "puntaje según dificultad" rule lives.
/// A target's score is not authored: it is fixed by the mission's difficulty as
/// <c>score = <see cref="BaseTargetScore"/> × difficultyFactor</c> (factor ∈ {1,2,3} for
/// Beginner/Intermediate/Advanced). The award is therefore computed from the base score and the
/// difficulty factor the resolution carries, and the accepted-outcome value is deliberately not used —
/// which makes this a genuinely different function from the trivia/penalty strategies (they award the
/// value and ignore the factor).
/// </summary>
public sealed class TargetScorePolicy : IScorePolicy
{
    // Shared domain constant mirrored from MissionDesign's ScoreValue.BaseTargetScore. Held here so
    // the difficulty rule is expressed inside scoring without a dependency on the MissionDesign
    // assembly. Keep in sync with MissionDesign authoring.
    internal const int BaseTargetScore = 50;

    public ScoreValue Award(ScoreValue acceptedOutcomeScoreValue, int difficultyFactor)
    {
        ArgumentNullException.ThrowIfNull(acceptedOutcomeScoreValue);

        if (difficultyFactor <= 0)
        {
            throw new InvalidDifficultyFactorException(difficultyFactor);
        }

        return ScoreValue.Create(BaseTargetScore * difficultyFactor);
    }
}
