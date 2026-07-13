using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;

/// <summary>
/// First generic link: admits only a current, non-blocked runtime participant of the submitted team.
/// </summary>
public sealed class RuntimeParticipationLink : EvidenceIntakeValidationLink
{
    private readonly IRuntimeParticipationGuard _runtimeParticipationGuard;

    public RuntimeParticipationLink(IRuntimeParticipationGuard runtimeParticipationGuard)
    {
        _runtimeParticipationGuard = runtimeParticipationGuard;
    }

    protected override Task CheckAsync(
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken)
    {
        return _runtimeParticipationGuard.EnsureAllowedAsync(
            context.Session.LiveSessionId,
            context.TeamId,
            context.Token,
            cancellationToken);
    }
}
