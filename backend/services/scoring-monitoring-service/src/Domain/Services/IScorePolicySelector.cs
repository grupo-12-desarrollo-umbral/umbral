using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Resolves the score-calculation <see cref="IScorePolicy"/> to apply for a given
/// <see cref="ScoreSourceType"/> (session mode / domain variation). This is the runtime selection
/// step of the scoring Strategy pattern.
/// </summary>
public interface IScorePolicySelector
{
    IScorePolicy For(ScoreSourceType sourceType);
}
