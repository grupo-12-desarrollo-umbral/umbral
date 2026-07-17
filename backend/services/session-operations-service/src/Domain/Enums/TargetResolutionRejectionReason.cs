namespace umbral_backend.Domain.Enums;

public enum TargetResolutionRejectionReason
{
    ScannedValueDoesNotResolveToTarget = 1,
    TargetOutsideActiveSubstage = 2,
    TargetAlreadyResolvedByTeam = 3,
    ScannedValueResolvesToMultipleTargets = 4
}

public static class TargetResolutionRejectionReasonExtensions
{
    public static string ToMessage(this TargetResolutionRejectionReason reason)
    {
        return reason switch
        {
            TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget =>
                "The scanned value does not resolve to a target.",
            TargetResolutionRejectionReason.TargetOutsideActiveSubstage =>
                "The resolved target does not belong to the active treasure-hunt substage.",
            TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam =>
                "The target has already been resolved by this team.",
            TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets =>
                "The scanned value matches more than one target in this mission and cannot be resolved.",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown target-resolution rejection reason.")
        };
    }
}
