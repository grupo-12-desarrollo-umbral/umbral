namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCapacityReachedException : Exception
{
    public TeamCapacityReachedException(Guid teamId, int capacity)
        : base($"Team '{teamId}' has reached its capacity of {capacity} participants.")
    {
    }
}
