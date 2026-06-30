namespace umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;

public sealed class ArchiveTriviaQuizCommandValidator : AbstractValidator<ArchiveTriviaQuizCommand>
{
    public ArchiveTriviaQuizCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
