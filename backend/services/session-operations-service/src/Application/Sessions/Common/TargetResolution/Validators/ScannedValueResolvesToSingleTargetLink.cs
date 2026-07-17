using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.TargetResolution.Validators;

/// <summary>
/// Rejects a scan whose value matches more than one snapshotted target. Runs ahead of
/// <see cref="TargetExistsForScanLink"/>: an ambiguous scan resolves to no single target, so
/// without this link it would be misreported as an unknown QR code.
/// </summary>
public sealed class ScannedValueResolvesToSingleTargetLink : TargetResolutionLink
{
    protected override Task<TargetResolutionRejectionReason?> CheckAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult<TargetResolutionRejectionReason?>(context.IsAmbiguous
            ? TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets
            : null);
}
