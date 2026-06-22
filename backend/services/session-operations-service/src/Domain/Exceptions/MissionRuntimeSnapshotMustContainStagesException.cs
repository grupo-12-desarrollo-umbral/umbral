namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotMustContainStagesException : DomainException
{
    public MissionRuntimeSnapshotMustContainStagesException()
        : base("A mission runtime snapshot must contain at least one stage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
