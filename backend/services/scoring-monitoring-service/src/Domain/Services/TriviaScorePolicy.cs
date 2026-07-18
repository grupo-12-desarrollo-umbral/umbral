using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Trivia score-calculation strategy (mode = trivia). Awards the question's authored point value
/// unchanged: trivia scores are configured per question in authoring and are not difficulty-weighted,
/// so this strategy passes the accepted outcome value through and ignores the difficulty factor.
/// </summary>
public sealed class TriviaScorePolicy : IScorePolicy
{
    public ScoreValue Award(ScoreValue acceptedOutcomeScoreValue, int difficultyFactor)
    {
        ArgumentNullException.ThrowIfNull(acceptedOutcomeScoreValue);
        return acceptedOutcomeScoreValue;
    }
}
