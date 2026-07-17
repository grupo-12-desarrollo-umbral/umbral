using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services.SessionStates;

namespace umbral_backend.Domain.Services;

public sealed class SessionStateTransitionPolicy
{
    public void EnsureCanTransition(SessionState currentState, SessionState nextState, int associatedTeamCount)
    {
        // Structural reachability is checked first — it's the more fundamental gate — so this
        // matches the Application-layer CurrentStateGate's order and a rejected request reports the
        // same reason regardless of entry path.
        if (!IsTransitionAllowed(currentState, nextState))
        {
            throw new InvalidSessionStateTransitionException(currentState, nextState);
        }

        if (nextState == SessionState.Active && associatedTeamCount <= 0)
        {
            throw new LiveSessionRequiresAtLeastOneTeamException();
        }
    }

    public bool IsTransitionAllowed(SessionState currentState, SessionState nextState)
    {
        return LiveSessionStateFactory.For(currentState).CanTransitionTo(nextState);
    }
}
