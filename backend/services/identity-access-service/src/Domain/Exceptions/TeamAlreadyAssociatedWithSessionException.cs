namespace umbral_backend.Domain.Exceptions;

public sealed class TeamAlreadyAssociatedWithSessionException : Exception
{
    public TeamAlreadyAssociatedWithSessionException(Guid liveSessionId, Guid teamId)
        : base($"Team '{teamId}' is already associated with live session '{liveSessionId}'.")
    {
    }
}
