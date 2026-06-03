namespace umbral_backend.Application.Sessions.Commands.CreateTriviaSession;

public sealed class CreateTriviaSessionCommandValidator : AbstractValidator<CreateTriviaSessionCommand>
{
    public CreateTriviaSessionCommandValidator()
    {
        RuleFor(command => command.SourceTriviaQuizId)
            .GreaterThan(0);

        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.MaximumTimeMinutes)
            .GreaterThan(0);

        RuleFor(command => command.ScheduledAt)
            .NotEqual(default(DateTimeOffset));
    }
}
