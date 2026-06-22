namespace umbral_backend.Domain.Exceptions;

public sealed class TeamJoinClosedException : DomainException
{
    public TeamJoinClosedException(Guid teamId)
        : base($"Team '{teamId}' is not accepting new participants.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
