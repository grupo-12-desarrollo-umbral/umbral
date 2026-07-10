namespace umbral_backend.Application.Sessions.Commands.SubmitTriviaAnswer;

public sealed class SubmitTriviaAnswerCommandValidator : AbstractValidator<SubmitTriviaAnswerCommand>
{
    public SubmitTriviaAnswerCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.TriviaSubstageSnapshotId)
            .NotEmpty();

        // Snapshot sequence orders are 1-based (the runtime snapshot never emits order 0).
        RuleFor(command => command.QuestionSequenceOrder)
            .GreaterThan(0);

        RuleFor(command => command.SelectedOptionSequenceOrder)
            .GreaterThan(0);
    }
}
