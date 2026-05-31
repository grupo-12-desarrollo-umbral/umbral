namespace umbral_backend.Domain.Exceptions;

public sealed class TeamAlreadyDeactivatedException : Exception
{
    public TeamAlreadyDeactivatedException(Guid teamId)
        : base($"Team '{teamId}' is already deactivated.")
    {
    }
}
