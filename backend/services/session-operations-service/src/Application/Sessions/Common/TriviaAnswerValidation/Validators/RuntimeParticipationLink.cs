using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;

/// <summary>
/// Link 1 (first): the caller must still be an admitted, non-blocked runtime participant of the
/// team. Reuses the fail-closed <see cref="IRuntimeParticipationGuard"/> (#91) — on deny it records
/// the Participation Block and throws, short-circuiting before any question/window/duplicate check.
/// </summary>
public sealed class RuntimeParticipationLink : TriviaAnswerValidationLink
{
    private readonly IRuntimeParticipationGuard _runtimeParticipationGuard;

    public RuntimeParticipationLink(IRuntimeParticipationGuard runtimeParticipationGuard)
    {
        _runtimeParticipationGuard = runtimeParticipationGuard;
    }

    protected override Task CheckAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
    {
        return _runtimeParticipationGuard.EnsureAllowedAsync(
            context.Session.LiveSessionId,
            context.TeamId,
            context.Token,
            cancellationToken);
    }
}
