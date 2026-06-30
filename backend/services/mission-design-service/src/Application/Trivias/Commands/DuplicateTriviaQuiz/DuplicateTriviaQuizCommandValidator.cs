namespace umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;

public sealed class DuplicateTriviaQuizCommandValidator : AbstractValidator<DuplicateTriviaQuizCommand>
{
    public DuplicateTriviaQuizCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
