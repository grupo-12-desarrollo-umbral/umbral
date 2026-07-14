using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

public interface IScorePolicy
{
    ScoreValue Award(ScoreValue acceptedOutcomeScoreValue);
}
