using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.TargetResolution.Validators;

public sealed class TargetExistsForScanLink : TargetResolutionLink
{
    protected override Task<TargetResolutionRejectionReason?> CheckAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult<TargetResolutionRejectionReason?>(context.ResolvedTarget is null
            ? TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget
            : null);
}
