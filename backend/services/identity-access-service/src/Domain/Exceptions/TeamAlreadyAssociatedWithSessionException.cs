namespace umbral_backend.Domain.Exceptions;

public sealed class TeamAlreadyAssociatedWithSessionException : DomainException
{
    public TeamAlreadyAssociatedWithSessionException(Guid liveSessionId, Guid teamId)
        : base($"Team '{teamId}' is already associated with live session '{liveSessionId}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
