using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.TargetResolution.Validators;

public sealed class TargetBelongsToActiveSubstageLink : TargetResolutionLink
{
    protected override Task<TargetResolutionRejectionReason?> CheckAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken)
    {
        var target = context.ResolvedTarget;
        var belongs = target is not null &&
            target.SubstageSnapshotId == context.ActiveSubstageId &&
            target.IsActive;
        return Task.FromResult<TargetResolutionRejectionReason?>(belongs
            ? null
            : TargetResolutionRejectionReason.TargetOutsideActiveSubstage);
    }
}
