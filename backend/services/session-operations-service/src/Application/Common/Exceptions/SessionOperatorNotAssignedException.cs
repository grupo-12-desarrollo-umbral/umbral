namespace umbral_backend.Application.Common.Exceptions;

public sealed class SessionOperatorNotAssignedException : Exception
{
    public SessionOperatorNotAssignedException(Guid liveSessionId)
        : base($"Session '{liveSessionId}' cannot change state until an operator is assigned.")
    {
    }
}
