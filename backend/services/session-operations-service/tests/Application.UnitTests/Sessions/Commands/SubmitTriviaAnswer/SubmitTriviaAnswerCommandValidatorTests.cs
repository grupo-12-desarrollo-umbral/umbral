using umbral_backend.Application.Sessions.Commands.SubmitTriviaAnswer;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.SubmitTriviaAnswer;

public sealed class SubmitTriviaAnswerCommandValidatorTests
{
    private readonly SubmitTriviaAnswerCommandValidator _validator = new();

    private static SubmitTriviaAnswerCommand Valid() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1);

    [Fact]
    public void Validate_WithWellFormedCommand_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyLiveSessionId_Fails()
    {
        _validator.Validate(Valid() with { LiveSessionId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyTeamId_Fails()
    {
        _validator.Validate(Valid() with { TeamId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyTriviaSubstageSnapshotId_Fails()
    {
        _validator.Validate(Valid() with { TriviaSubstageSnapshotId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveQuestionSequenceOrder_Fails(int order)
    {
        _validator.Validate(Valid() with { QuestionSequenceOrder = order }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveSelectedOptionSequenceOrder_Fails(int order)
    {
        _validator.Validate(Valid() with { SelectedOptionSequenceOrder = order }).IsValid.Should().BeFalse();
    }
}
