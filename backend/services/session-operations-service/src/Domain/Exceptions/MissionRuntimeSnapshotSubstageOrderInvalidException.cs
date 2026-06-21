namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotSubstageOrderInvalidException : Exception
{
    public MissionRuntimeSnapshotSubstageOrderInvalidException()
        : base("Mission runtime snapshot substages must follow strict mission order within their stage.")
    {
    }
}
