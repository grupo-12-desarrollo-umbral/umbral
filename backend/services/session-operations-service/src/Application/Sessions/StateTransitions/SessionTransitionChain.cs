namespace umbral_backend.Application.Sessions.StateTransitions;

/// <summary>
/// Builds the ordered Chain of Responsibility from the registered validators and runs it.
/// Order follows DI registration order, so downstream HUs (timer, round orchestration) add a
/// new <see cref="SessionTransitionValidator"/> registration without touching this pipeline.
/// </summary>
public sealed class SessionTransitionChain
{
    private readonly SessionTransitionValidator? _head;

    public SessionTransitionChain(IEnumerable<SessionTransitionValidator> validators)
    {
        SessionTransitionValidator? head = null;
        SessionTransitionValidator? previous = null;

        foreach (var validator in validators)
        {
            if (head is null)
            {
                head = validator;
            }
            else
            {
                previous!.SetNext(validator);
            }

            previous = validator;
        }

        _head = head;
    }

    public Task ValidateAsync(SessionTransitionContext context, CancellationToken cancellationToken)
    {
        return _head?.ValidateAsync(context, cancellationToken) ?? Task.CompletedTask;
    }
}
