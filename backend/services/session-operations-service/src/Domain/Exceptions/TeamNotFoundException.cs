namespace umbral_backend.Domain.Exceptions;

public sealed class TeamNotFoundException : Exception
{
    public TeamNotFoundException(Guid teamId)
        : base($"Team '{teamId}' was not found in the live session.")
    {
    }
}
