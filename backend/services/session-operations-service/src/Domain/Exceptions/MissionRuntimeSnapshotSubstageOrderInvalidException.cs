namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotSubstageOrderInvalidException : DomainException
{
    public MissionRuntimeSnapshotSubstageOrderInvalidException()
        : base("Mission runtime snapshot substages must follow strict mission order within their stage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
