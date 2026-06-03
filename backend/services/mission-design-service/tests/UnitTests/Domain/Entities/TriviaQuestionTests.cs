using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class TriviaQuestionTests
{
    [Fact]
    public void Create_WithQuestionAuthoringFields_PreservesPromptOrderingAndAssociatedOptions()
    {
        var options = new[]
        {
            TriviaOption.Create("Option A", 1, true),
            TriviaOption.Create("Option B", 2, false)
        };

        var question = TriviaQuestion.Create(" Question prompt ", 3, 50, 30, " Because it matches the baseline. ", options, isActive: false);

        question.Prompt.Should().Be("Question prompt");
        question.SequenceOrder.Should().Be(3);
        question.ScoreValue.Should().Be(50);
        question.TimeLimit.Should().Be(QuestionTimer.Create(30));
        question.Explanation.Should().Be("Because it matches the baseline.");
        question.IsActive.Should().BeFalse();
        question.Options.Should().ContainInOrder(options);
    }

    [Fact]
    public void Create_WithoutQuestionAuthoringFields_PreservesLegacyBaseline()
    {
        var question = TriviaQuestion.Create(" Question prompt ", 3);

        question.ScoreValue.Should().BeNull();
        question.TimeLimit.Should().BeNull();
        question.Explanation.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenPromptIsInvalid_Throws(string? prompt)
    {
        var act = () => TriviaQuestion.Create(prompt!, 1);

        act.Should().Throw<TriviaQuestionPromptRequiredException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenSequenceOrderIsInvalid_Throws(int sequenceOrder)
    {
        var act = () => TriviaQuestion.Create("Prompt", sequenceOrder);

        act.Should().Throw<TriviaQuestionSequenceOrderMustBePositiveException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Create_WhenScoreValueIsWithinRange_SetsValue(int scoreValue)
    {
        var question = TriviaQuestion.Create("Prompt", 1, scoreValue, 30, null);

        question.ScoreValue.Should().Be(scoreValue);
    }

    [Fact]
    public void Create_WhenScoreValueIsZero_ThrowsPositiveException()
    {
        var act = () => TriviaQuestion.Create("Prompt", 1, 0, 30, null);

        act.Should().Throw<TriviaQuestionScoreValueMustBePositiveException>();
    }

    [Fact]
    public void Create_WhenScoreValueExceedsMaximum_ThrowsMaximumException()
    {
        var act = () => TriviaQuestion.Create("Prompt", 1, 101, 30, null);

        act.Should().Throw<TriviaQuestionScoreValueExceedsMaximumException>();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(120)]
    public void Create_WhenTimeLimitIsWithinRange_SetsValue(int timeLimitSeconds)
    {
        var question = TriviaQuestion.Create("Prompt", 1, 10, timeLimitSeconds, null);

        question.TimeLimit.Should().Be(QuestionTimer.Create(timeLimitSeconds));
    }

    [Fact]
    public void Create_WhenTimeLimitIsBelowMinimum_ThrowsPositiveException()
    {
        var act = () => TriviaQuestion.Create("Prompt", 1, 10, 4, null);

        act.Should().Throw<QuestionTimerMustBePositiveException>();
    }

    [Fact]
    public void Create_WhenTimeLimitExceedsMaximum_ThrowsMaximumException()
    {
        var act = () => TriviaQuestion.Create("Prompt", 1, 10, 121, null);

        act.Should().Throw<QuestionTimerExceedsMaximumException>();
    }
}
