using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;

/// <summary>
/// First generic link: admits only a current, non-blocked runtime participant of the submitted team.
/// </summary>
public sealed class RuntimeParticipationLink : EvidenceIntakeValidationLink
{
    private readonly IRuntimeParticipationGuard _runtimeParticipationGuard;
    private readonly IParticipantSessionMembershipChecker _membershipChecker;

    public RuntimeParticipationLink(
        IRuntimeParticipationGuard runtimeParticipationGuard,
        IParticipantSessionMembershipChecker membershipChecker)
    {
        _runtimeParticipationGuard = runtimeParticipationGuard;
        _membershipChecker = membershipChecker;
    }

    protected override async Task CheckAsync(
        EvidenceIntakeValidationContext context,
        CancellationToken cancellationToken)
    {
        await _runtimeParticipationGuard.EnsureAllowedAsync(
            context.Session.LiveSessionId,
            cancellationToken);

        if (!_membershipChecker.Check(context.Session, context.TeamId).IsAllowed)
        {
            throw new ForbiddenAccessException();
        }
    }
}
