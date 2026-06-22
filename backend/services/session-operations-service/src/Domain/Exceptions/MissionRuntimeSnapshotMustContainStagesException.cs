namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotMustContainStagesException : Exception
{
    public MissionRuntimeSnapshotMustContainStagesException()
        : base("A mission runtime snapshot must contain at least one stage.")
    {
    }
}
