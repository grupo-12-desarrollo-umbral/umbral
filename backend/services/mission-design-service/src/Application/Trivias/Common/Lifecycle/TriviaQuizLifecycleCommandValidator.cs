namespace umbral_backend.Application.Trivias.Common.Lifecycle;

public abstract class TriviaQuizLifecycleCommandValidator<TCommand> : AbstractValidator<TCommand>
    where TCommand : ITriviaQuizLifecycleCommand
{
    protected TriviaQuizLifecycleCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);

        AddOperationSpecificRules();
    }

    protected virtual void AddOperationSpecificRules()
    {
    }
}
