using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScorePolicySelectorTests
{
    private readonly ScorePolicySelector _selector = new(
        new TargetScorePolicy(),
        new TriviaScorePolicy(),
        new PenaltyScorePolicy());

    [Fact]
    public void For_TargetResolution_ReturnsTargetScorePolicy()
    {
        _selector.For(ScoreSourceType.TargetResolution).Should().BeOfType<TargetScorePolicy>();
    }

    [Fact]
    public void For_TriviaAnswerSubmission_ReturnsTriviaScorePolicy()
    {
        _selector.For(ScoreSourceType.TriviaAnswerSubmission).Should().BeOfType<TriviaScorePolicy>();
    }

    [Fact]
    public void For_Penalty_ReturnsPenaltyScorePolicy()
    {
        _selector.For(ScoreSourceType.Penalty).Should().BeOfType<PenaltyScorePolicy>();
    }

    [Fact]
    public void For_UnknownSourceType_ThrowsUnsupportedScoreSourceType()
    {
        var act = () => _selector.For((ScoreSourceType)999);

        act.Should().Throw<UnsupportedScoreSourceTypeException>();
    }
}
