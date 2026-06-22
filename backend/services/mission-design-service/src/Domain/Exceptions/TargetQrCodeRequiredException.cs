namespace umbral_backend.Domain.Exceptions;

public sealed class TargetQrCodeRequiredException : DomainException
{
    public TargetQrCodeRequiredException()
        : base("Target QR code is required for validation.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
