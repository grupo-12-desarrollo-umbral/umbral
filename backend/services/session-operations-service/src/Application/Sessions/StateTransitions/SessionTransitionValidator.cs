namespace umbral_backend.Application.Sessions.StateTransitions;

/// <summary>
/// Chain of Responsibility link guarding a single session-state transition concern.
/// Each link runs its own <see cref="CheckAsync"/> and, on success, delegates to the next
/// link. The first failing link throws and short-circuits the rest of the chain.
/// </summary>
public abstract class SessionTransitionValidator
{
    private SessionTransitionValidator? _next;

    public SessionTransitionValidator SetNext(SessionTransitionValidator next)
    {
        _next = next;
        return next;
    }

    public async Task ValidateAsync(SessionTransitionContext context, CancellationToken cancellationToken)
    {
        await CheckAsync(context, cancellationToken);

        if (_next is not null)
        {
            await _next.ValidateAsync(context, cancellationToken);
        }
    }

    protected abstract Task CheckAsync(SessionTransitionContext context, CancellationToken cancellationToken);
}
