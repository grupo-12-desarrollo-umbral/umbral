using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;

/// <summary>
/// Third generic link: evidence requires a synchronized active-substage pointer that resolves in the
/// frozen mission runtime snapshot. Concrete forms decide whether the caller's declared target or
/// question belongs to that active substage.
/// </summary>
public sealed class ActiveSubstagePresentLink : EvidenceIntakeValidationLink
{
    protected override Task CheckAsync(
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken)
    {
        var activeSubstageId = context.Session.ActiveSubstageId;
        var activeSubstageExists = activeSubstageId.HasValue &&
            context.Session.MissionRuntimeSnapshot.StageSnapshots
                .SelectMany(stage => stage.SubstageSnapshots)
                .Any(substage => substage.SubstageSnapshotId == activeSubstageId.Value);

        if (!activeSubstageExists)
        {
            throw new EvidenceSubmissionContextRequiredException();
        }

        return Task.CompletedTask;
    }
}
