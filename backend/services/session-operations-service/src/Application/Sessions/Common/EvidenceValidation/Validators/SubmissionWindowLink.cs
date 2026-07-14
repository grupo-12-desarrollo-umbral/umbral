using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.EvidenceValidation.Validators;

/// <summary>
/// Enforces the active treasure-hunt substage window. Trivia retains its form-specific active-question
/// timer rule, so this common link deliberately does not reinterpret late trivia answers.
/// </summary>
public sealed class SubmissionWindowLink : EvidenceValidationLink
{
    protected override Task CheckAsync(
        EvidenceValidationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Submission.SubmissionType == EvidenceSubmissionType.TreasureHuntQrScan &&
            context.Session.GetAuthoritativeSessionTimerSnapshot(context.SubmittedAt).IsExpired)
        {
            throw new EvidenceContextRejectedException(EvidenceRejectionReason.OutsideSubmissionWindow);
        }

        return Task.CompletedTask;
    }
}
