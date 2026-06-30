namespace umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;

public sealed class RetireTriviaQuizCommandValidator : AbstractValidator<RetireTriviaQuizCommand>
{
    public RetireTriviaQuizCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
