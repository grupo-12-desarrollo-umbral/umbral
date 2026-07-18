using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Penalty score-calculation strategy (domain variation = penalty). Awards the deduction magnitude
/// the operator applied unchanged and ignores the difficulty factor; the impact is recorded as a
/// penalty ledger entry upstream.
/// </summary>
public sealed class PenaltyScorePolicy : IScorePolicy
{
    public ScoreValue Award(ScoreValue acceptedOutcomeScoreValue, int difficultyFactor)
    {
        ArgumentNullException.ThrowIfNull(acceptedOutcomeScoreValue);
        return acceptedOutcomeScoreValue;
    }
}
