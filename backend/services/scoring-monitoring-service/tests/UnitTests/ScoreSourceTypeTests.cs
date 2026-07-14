using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScoreSourceTypeTests
{
    [Fact]
    public void Enum_ShouldExposeSupportedScoreOrigins()
    {
        Enum.GetValues<ScoreSourceType>().Should().Equal(
            ScoreSourceType.TargetResolution,
            ScoreSourceType.TriviaAnswerSubmission,
            ScoreSourceType.Penalty);
    }
}
