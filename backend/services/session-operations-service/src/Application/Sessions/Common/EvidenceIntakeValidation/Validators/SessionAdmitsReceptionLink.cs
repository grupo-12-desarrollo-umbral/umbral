using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;

/// <summary>
/// Second generic link: only an active session admits gameplay evidence. The aggregate reasserts
/// this state invariant during registration.
/// </summary>
public sealed class SessionAdmitsReceptionLink : EvidenceIntakeValidationLink
{
    protected override Task CheckAsync(
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session.State != SessionState.Active)
        {
            throw new TriviaAnswerRequiresActiveSessionException(context.Session.State);
        }

        return Task.CompletedTask;
    }
}
