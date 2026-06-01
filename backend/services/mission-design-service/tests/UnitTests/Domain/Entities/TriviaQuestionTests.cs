using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class TriviaQuestionTests
{
    [Fact]
    public void Create_PreservesPromptOrderingAndAssociatedOptions()
    {
        var options = new[]
        {
            TriviaOption.Create("Option A", 1, true),
            TriviaOption.Create("Option B", 2, false)
        };

        var question = TriviaQuestion.Create(" Question prompt ", 3, options, isActive: false);

        question.Prompt.Should().Be("Question prompt");
        question.SequenceOrder.Should().Be(3);
        question.IsActive.Should().BeFalse();
        question.Options.Should().ContainInOrder(options);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenPromptIsInvalid_Throws(string? prompt)
    {
        var act = () => TriviaQuestion.Create(prompt, 1);

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
}
