namespace umbral_backend.Domain.Exceptions;

public sealed class MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException : DomainException
{
    public MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException()
        : base("Target QR identifiers must be unique within a mission runtime snapshot.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
