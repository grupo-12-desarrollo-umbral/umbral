namespace umbral_backend.Domain.Exceptions;

public sealed class TargetQrCodeRequiredException : Exception
{
    public TargetQrCodeRequiredException()
        : base("Target QR code is required for validation.")
    {
    }
}
