namespace umbral_backend.Domain.Exceptions;

public sealed class TeamJoinClosedException : Exception
{
    public TeamJoinClosedException(Guid teamId)
        : base($"Team '{teamId}' is not accepting new participants.")
    {
    }
}
