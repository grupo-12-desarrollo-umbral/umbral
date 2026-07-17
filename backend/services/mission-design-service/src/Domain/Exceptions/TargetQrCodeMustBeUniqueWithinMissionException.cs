namespace umbral_backend.Domain.Exceptions;

public sealed class TargetQrCodeMustBeUniqueWithinMissionException : DomainException
{
    public TargetQrCodeMustBeUniqueWithinMissionException()
        : base("Target QR codes must be unique within a mission.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string? PublicDetail => "Target QR codes must be unique within a mission.";
}
