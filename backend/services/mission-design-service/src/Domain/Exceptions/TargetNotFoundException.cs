namespace umbral_backend.Domain.Exceptions;

public sealed class TargetNotFoundException : DomainException
{
    public TargetNotFoundException(int targetId)
        : base($"Target '{targetId}' was not found in the substage.")
    {
        TargetId = targetId;
    }

    public int TargetId { get; }

    public override ErrorCategory Category => ErrorCategory.NotFound;
}
