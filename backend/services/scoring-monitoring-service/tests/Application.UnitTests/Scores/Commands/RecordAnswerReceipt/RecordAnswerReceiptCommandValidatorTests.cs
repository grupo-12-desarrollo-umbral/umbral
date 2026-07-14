using FluentValidation.TestHelper;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

namespace umbral_backend.Application.UnitTests.Scores.Commands.RecordAnswerReceipt;

public sealed class RecordAnswerReceiptCommandValidatorTests
{
    private readonly RecordAnswerReceiptCommandValidator _validator = new();

    private static RecordAnswerReceiptCommand Valid() => new(
        LiveSessionId: Guid.NewGuid(),
        TeamId: Guid.NewGuid(),
        TriviaAnswerSubmissionId: Guid.NewGuid(),
        TriviaSubstageSnapshotId: Guid.NewGuid(),
        QuestionSequenceOrder: 0,
        SelectedOptionSequenceOrder: 1,
        IsCorrect: true,
        ScoreValue: 10,
        SubmittedAt: DateTimeOffset.UtcNow);

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLiveSessionId_Fails()
    {
        _validator.TestValidate(Valid() with { LiveSessionId = Guid.Empty })
            .ShouldHaveValidationErrorFor(command => command.LiveSessionId);
    }

    [Fact]
    public void Validate_EmptyTeamId_Fails()
    {
        _validator.TestValidate(Valid() with { TeamId = Guid.Empty })
            .ShouldHaveValidationErrorFor(command => command.TeamId);
    }

    [Fact]
    public void Validate_EmptyTriviaAnswerSubmissionId_Fails()
    {
        _validator.TestValidate(Valid() with { TriviaAnswerSubmissionId = Guid.Empty })
            .ShouldHaveValidationErrorFor(command => command.TriviaAnswerSubmissionId);
    }

    [Fact]
    public void Validate_EmptyTriviaSubstageSnapshotId_Fails()
    {
        _validator.TestValidate(Valid() with { TriviaSubstageSnapshotId = Guid.Empty })
            .ShouldHaveValidationErrorFor(command => command.TriviaSubstageSnapshotId);
    }

    [Fact]
    public void Validate_NegativeQuestionSequenceOrder_Fails()
    {
        _validator.TestValidate(Valid() with { QuestionSequenceOrder = -1 })
            .ShouldHaveValidationErrorFor(command => command.QuestionSequenceOrder);
    }

    [Fact]
    public void Validate_NegativeSelectedOptionSequenceOrder_Fails()
    {
        _validator.TestValidate(Valid() with { SelectedOptionSequenceOrder = -1 })
            .ShouldHaveValidationErrorFor(command => command.SelectedOptionSequenceOrder);
    }

    [Fact]
    public void Validate_NegativeScoreValue_Fails()
    {
        _validator.TestValidate(Valid() with { ScoreValue = -5 })
            .ShouldHaveValidationErrorFor(command => command.ScoreValue);
    }
}
