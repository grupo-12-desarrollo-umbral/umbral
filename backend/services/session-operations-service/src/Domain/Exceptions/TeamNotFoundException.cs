namespace umbral_backend.Domain.Exceptions;

public sealed class TeamNotFoundException : DomainException
{
    public TeamNotFoundException(Guid teamId)
        : base($"Team '{teamId}' was not found in the live session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.NotFound;
}
