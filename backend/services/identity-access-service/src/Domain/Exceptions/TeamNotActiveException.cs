namespace umbral_backend.Domain.Exceptions;

public sealed class TeamNotActiveException : DomainException
{
    public TeamNotActiveException(Guid teamId)
        : base($"Team '{teamId}' is not active.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
