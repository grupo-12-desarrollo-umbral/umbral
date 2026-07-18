using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Score-calculation Strategy. Each implementation computes the points to award for one session mode /
/// domain variation. The two inputs let the strategies be genuinely different functions: the trivia
/// and penalty strategies award the accepted outcome value and ignore difficulty, while the
/// treasure-hunt strategy derives the award from the mission difficulty factor (base × factor) and
/// ignores the incoming value — so which strategy the selector picks changes the result.
/// </summary>
public interface IScorePolicy
{
    ScoreValue Award(ScoreValue acceptedOutcomeScoreValue, int difficultyFactor);
}
