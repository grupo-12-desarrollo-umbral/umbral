using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Selects the score-calculation Strategy for a given <see cref="ScoreSourceType"/>: treasure-hunt
/// targets are difficulty-weighted (<see cref="TargetScorePolicy"/>), trivia awards the question value
/// (<see cref="TriviaScorePolicy"/>), and penalties award the deduction magnitude
/// (<see cref="PenaltyScorePolicy"/>).
/// </summary>
public sealed class ScorePolicySelector : IScorePolicySelector
{
    private readonly IReadOnlyDictionary<ScoreSourceType, IScorePolicy> _policies;

    public ScorePolicySelector(
        TargetScorePolicy targetScorePolicy,
        TriviaScorePolicy triviaScorePolicy,
        PenaltyScorePolicy penaltyScorePolicy)
    {
        ArgumentNullException.ThrowIfNull(targetScorePolicy);
        ArgumentNullException.ThrowIfNull(triviaScorePolicy);
        ArgumentNullException.ThrowIfNull(penaltyScorePolicy);

        _policies = new Dictionary<ScoreSourceType, IScorePolicy>
        {
            [ScoreSourceType.TargetResolution] = targetScorePolicy,
            [ScoreSourceType.TriviaAnswerSubmission] = triviaScorePolicy,
            [ScoreSourceType.Penalty] = penaltyScorePolicy,
        };
    }

    public IScorePolicy For(ScoreSourceType sourceType)
    {
        if (_policies.TryGetValue(sourceType, out var policy))
        {
            return policy;
        }

        throw new UnsupportedScoreSourceTypeException(sourceType);
    }
}
