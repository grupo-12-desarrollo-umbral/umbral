using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public sealed class SessionStateTransitionPolicy
{
    public void EnsureCanTransition(SessionState currentState, SessionState nextState, int teamCount)
    {
        if (nextState == SessionState.Active && teamCount <= 0)
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
        return currentState switch
        {
            SessionState.Scheduled => nextState is SessionState.Preparing or SessionState.Cancelled,
            SessionState.Preparing => nextState is SessionState.Active or SessionState.Cancelled,
            SessionState.Active => nextState is SessionState.Paused or SessionState.Finished or SessionState.Cancelled,
            SessionState.Paused => nextState is SessionState.Active or SessionState.Finished or SessionState.Cancelled,
            SessionState.Finished => false,
            SessionState.Cancelled => false,
            _ => false
        };
    }
}
