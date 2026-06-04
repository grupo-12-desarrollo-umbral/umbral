using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.StateTransitions.Validators;

/// <summary>
/// Second gate: a session cannot be brought toward going live without an operator owning it.
/// </summary>
public sealed class OperatorAssignmentGate : SessionTransitionValidator
{
    protected override Task CheckAsync(SessionTransitionContext context, CancellationToken cancellationToken)
    {
        var requiresOperator = context.TargetState is SessionState.Preparing or SessionState.Active;

        if (requiresOperator && context.Session.AssignedOperatorUserId is null)
        {
            throw new SessionOperatorNotAssignedException(context.Session.LiveSessionId);
        }

        return Task.CompletedTask;
    }
}
