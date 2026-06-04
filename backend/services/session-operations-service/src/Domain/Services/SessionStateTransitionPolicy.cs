using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services.SessionStates;

namespace umbral_backend.Domain.Services;

public sealed class SessionStateTransitionPolicy
{
    public void EnsureCanTransition(SessionState currentState, SessionState nextState, int associatedTeamCount)
    {
        if (nextState == SessionState.Active && associatedTeamCount <= 0)
        {
            throw new LiveSessionRequiresAtLeastOneTeamException();
        }

        if (!IsTransitionAllowed(currentState, nextState))
        {
            throw new InvalidSessionStateTransitionException(currentState, nextState);
        }
    }

    public bool IsTransitionAllowed(SessionState currentState, SessionState nextState)
    {
        return LiveSessionStateFactory.For(currentState).CanTransitionTo(nextState);
    }
}
