namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotStageOrderInvalidException : Exception
{
    public MissionRuntimeSnapshotStageOrderInvalidException()
        : base("Mission runtime snapshot stages must follow strict mission order.")
    {
    }
}
