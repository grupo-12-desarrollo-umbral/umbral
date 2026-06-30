namespace umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;

public sealed class PublishTriviaQuizCommandValidator : AbstractValidator<PublishTriviaQuizCommand>
{
    public PublishTriviaQuizCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
