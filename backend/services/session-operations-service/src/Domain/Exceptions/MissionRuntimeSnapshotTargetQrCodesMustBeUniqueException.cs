namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException : Exception
{
    public MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException()
        : base("Target QR identifiers must be unique within a mission runtime snapshot.")
    {
    }
}
