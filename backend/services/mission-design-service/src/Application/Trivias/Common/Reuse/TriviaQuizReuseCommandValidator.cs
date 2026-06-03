namespace umbral_backend.Application.Trivias.Common.Reuse;

public abstract class TriviaQuizReuseCommandValidator<TCommand> : AbstractValidator<TCommand>
    where TCommand : ITriviaQuizReuseCommand
{
    protected TriviaQuizReuseCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
