using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class TriviaOptionTests
{
    [Fact]
    public void Create_SetsTrimmedValues()
    {
        var option = TriviaOption.Create(" Correct answer ", 2, true);

        option.OptionText.Should().Be("Correct answer");
        option.SequenceOrder.Should().Be(2);
        option.IsCorrect.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenOptionTextIsInvalid_Throws(string? optionText)
    {
        var act = () => TriviaOption.Create(optionText!, 1, false);

        act.Should().Throw<TriviaOptionTextRequiredException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenSequenceOrderIsInvalid_Throws(int sequenceOrder)
    {
        var act = () => TriviaOption.Create("Option", sequenceOrder, false);

        act.Should().Throw<TriviaOptionSequenceOrderMustBePositiveException>();
    }
}
