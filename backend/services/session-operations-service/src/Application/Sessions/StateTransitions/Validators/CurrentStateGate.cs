using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.StateTransitions.Validators;

/// <summary>
/// First gate: is the requested transition structurally reachable from the current state?
/// </summary>
public sealed class CurrentStateGate : SessionTransitionValidator
{
    private readonly SessionStateTransitionPolicy _transitionPolicy;

    public CurrentStateGate(SessionStateTransitionPolicy transitionPolicy)
    {
        _transitionPolicy = transitionPolicy;
    }

    protected override Task CheckAsync(SessionTransitionContext context, CancellationToken cancellationToken)
    {
        if (!_transitionPolicy.IsTransitionAllowed(context.Session.State, context.TargetState))
        {
            throw new InvalidSessionStateTransitionException(context.Session.State, context.TargetState);
        }

        return Task.CompletedTask;
    }
}
