namespace umbral_backend.Application.Trivias.Commands.RemoveTriviaQuestion;

public sealed class RemoveTriviaQuestionCommandValidator : AbstractValidator<RemoveTriviaQuestionCommand>
{
    public RemoveTriviaQuestionCommandValidator()
    {
        RuleFor(command => command.TriviaQuizId)
            .GreaterThan(0);

        RuleFor(command => command.QuestionId)
            .GreaterThan(0);
    }
}
