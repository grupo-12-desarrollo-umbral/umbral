using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.StateTransitions.Validators;

/// <summary>
/// Third gate: a session can only become <see cref="SessionState.Active"/> with at least one associated team.
/// </summary>
public sealed class ParticipantReadinessGate : SessionTransitionValidator
{
    protected override Task CheckAsync(SessionTransitionContext context, CancellationToken cancellationToken)
    {
        if (context.TargetState == SessionState.Active && context.Session.AssociatedTeamCount == 0)
        {
            throw new LiveSessionRequiresAtLeastOneTeamException();
        }

        return Task.CompletedTask;
    }
}
