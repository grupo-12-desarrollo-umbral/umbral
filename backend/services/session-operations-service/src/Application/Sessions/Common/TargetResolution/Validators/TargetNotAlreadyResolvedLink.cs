using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.TargetResolution.Validators;

public sealed class TargetNotAlreadyResolvedLink : TargetResolutionLink
{
    protected override Task<TargetResolutionRejectionReason?> CheckAsync(
        TargetResolutionContext context,
        CancellationToken cancellationToken)
    {
        var team = context.Session.Teams.SingleOrDefault(candidate =>
            candidate.TeamId == context.TeamId || candidate.ReferenceTeamId == context.TeamId);
        var duplicate = team is not null && context.ResolvedTarget is not null &&
            context.Session.TreasureEvidenceSubmissions.Any(submission =>
                submission.TeamId == team.TeamId &&
                submission.TargetSnapshotId == context.ResolvedTarget.TargetSnapshotId &&
                submission.ValidationState == EvidenceValidationState.Accepted);

        return Task.FromResult<TargetResolutionRejectionReason?>(duplicate
            ? TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam
            : null);
    }
}
