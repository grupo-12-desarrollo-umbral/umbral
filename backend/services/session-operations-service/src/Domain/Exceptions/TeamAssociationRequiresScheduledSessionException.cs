using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class TeamAssociationRequiresScheduledSessionException : Exception
{
    public TeamAssociationRequiresScheduledSessionException(SessionState currentState)
        : base($"Teams can only be associated while the session is Scheduled. Current state: {currentState}.")
    {
    }
}
