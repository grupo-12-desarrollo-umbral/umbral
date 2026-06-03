namespace umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;

public sealed class DeleteTriviaQuizCommandValidator : AbstractValidator<DeleteTriviaQuizCommand>
{
    public DeleteTriviaQuizCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
