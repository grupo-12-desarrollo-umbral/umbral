namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCapacityReachedException : DomainException
{
    public TeamCapacityReachedException(Guid teamId, int capacity)
        : base($"Team '{teamId}' has reached its capacity of {capacity} participants.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
