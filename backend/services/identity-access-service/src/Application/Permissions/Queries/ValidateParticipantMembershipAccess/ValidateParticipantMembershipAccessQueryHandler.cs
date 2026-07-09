using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

using umbral_backend.Application.Dtos.Permissions;
using umbral_backend.Application.Permissions.Common;

namespace umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

// Real subject wrapped by ParticipantMembershipAccessAuthorizationProxy (the registered IRequestHandler).
// Reaching here means the proxy already confirmed the actor is a Participant who belongs to the active
// team, so the membership decision is unconditionally Allow. Join-token issuance/consumption moved to
// session-operations (issue #87); Users stays the authority only for team-membership eligibility.
public sealed class ValidateParticipantMembershipAccessQueryHandler
{
    public Task<ParticipantMembershipAccessDecisionDto> Handle(
        ValidateParticipantMembershipAccessQuery query,
        CancellationToken cancellationToken)
    {
        var decision = AccessDecision.Allow(
            ProtectedCapability.ParticipantExperience,
            "Participant membership validated.");

        return Task.FromResult(new ParticipantMembershipAccessDecisionDto(
            decision.Capability.ToString(),
            decision.IsAllowed,
            ParticipantMembershipAccessReasonCodes.Eligible,
            decision.Reason,
            query.LiveSessionId,
            query.TeamId));
    }
}
