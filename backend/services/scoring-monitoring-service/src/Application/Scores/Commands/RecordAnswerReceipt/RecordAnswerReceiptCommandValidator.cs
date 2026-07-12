namespace umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

public sealed class RecordAnswerReceiptCommandValidator : AbstractValidator<RecordAnswerReceiptCommand>
{
    public RecordAnswerReceiptCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty();
        RuleFor(command => command.TeamId).NotEmpty();
        RuleFor(command => command.TriviaAnswerSubmissionId).NotEmpty();
        RuleFor(command => command.TriviaSubstageSnapshotId).NotEmpty();
        RuleFor(command => command.QuestionSequenceOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.SelectedOptionSequenceOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ScoreValue).GreaterThanOrEqualTo(0);
    }
}
