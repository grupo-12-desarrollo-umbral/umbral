using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

public sealed class SnapshotScorePolicy : IScorePolicy
{
    public ScoreValue Award(ScoreValue acceptedOutcomeScoreValue)
    {
        ArgumentNullException.ThrowIfNull(acceptedOutcomeScoreValue);
        return acceptedOutcomeScoreValue;
    }
}
