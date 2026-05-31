namespace umbral_backend.Domain.Exceptions;

public sealed class TeamNotActiveException : Exception
{
    public TeamNotActiveException(Guid teamId)
        : base($"Team '{teamId}' is not active.")
    {
    }
}
