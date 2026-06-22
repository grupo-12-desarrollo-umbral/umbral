namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotStageOrderInvalidException : DomainException
{
    public MissionRuntimeSnapshotStageOrderInvalidException()
        : base("Mission runtime snapshot stages must follow strict mission order.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
