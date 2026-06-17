namespace umbral_backend.Domain.Exceptions;

public sealed class TargetNotFoundException : Exception
{
    public TargetNotFoundException(int targetId)
        : base($"Target '{targetId}' was not found in the substage.")
    {
        TargetId = targetId;
    }

    public int TargetId { get; }
}
