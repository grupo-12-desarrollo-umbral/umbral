using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.EvidenceValidation.Validators;

/// <summary>
/// The registered evidence must remain bound to the resolved active MissionNode (substage).
/// </summary>
public sealed class ActiveSubstageBindingLink : EvidenceValidationLink
{
    protected override Task CheckAsync(
        EvidenceValidationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session.ActiveSubstageId != context.ActiveSubstageId ||
            context.Submission.ActiveSubstageId != context.ActiveSubstageId)
        {
            throw new EvidenceContextRejectedException(EvidenceRejectionReason.SubstageBindingMismatch);
        }

        return Task.CompletedTask;
    }
}
