using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common.EvidenceValidation.Validators;

/// <summary>
/// Validates a supplied form-origin discriminator against the registered evidence type. Existing
/// trivia intake has no origin signal, so a missing discriminator remains a pass-through until a
/// concrete form supplies one; a supplied, mismatched origin is rejected explicitly.
/// </summary>
public sealed class SubmissionOriginLink : EvidenceValidationLink
{
    protected override Task CheckAsync(
        EvidenceValidationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Origin is not null &&
            !string.Equals(
                context.Origin,
                context.Submission.SubmissionType.ToString(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new EvidenceContextRejectedException(EvidenceRejectionReason.UnauthorizedOrigin);
        }

        return Task.CompletedTask;
    }
}
