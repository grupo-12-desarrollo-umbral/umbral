using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

// Local session-team membership authority (finding #1). Given an already-loaded LiveSession, decides
// whether the authenticated caller is an active member of the requested team. Team lookup matches on
// ReferenceTeamId only, mirroring the participant-facing contract exactly.
public sealed class ParticipantSessionMembershipChecker : IParticipantSessionMembershipChecker
{
    private readonly ICurrentUser _currentUser;

    public ParticipantSessionMembershipChecker(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public ParticipantSessionMembershipResult Check(LiveSession liveSession, Guid teamId)
    {
        ArgumentNullException.ThrowIfNull(liveSession);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            return ParticipantSessionMembershipResult.Deny("unauthenticated");
        }

        var participant = liveSession.Participants
            .SingleOrDefault(p => p.ExternalIdentityId == externalIdentityId);

        if (participant is null)
        {
            return ParticipantSessionMembershipResult.Deny("participant-not-in-session");
        }

        if (participant.IsBlocked || participant.IsRemoved)
        {
            return ParticipantSessionMembershipResult.Deny("participant-blocked-or-removed");
        }

        var team = liveSession.Teams
            .SingleOrDefault(t => t.ReferenceTeamId == teamId);

        if (team is null)
        {
            return ParticipantSessionMembershipResult.Deny("team-not-in-session");
        }

        var isAssignedToTeam = team.Members
            .Any(m => m.SessionParticipantId == participant.SessionParticipantId && m.IsActive);

        return isAssignedToTeam
            ? ParticipantSessionMembershipResult.Allowed
            : ParticipantSessionMembershipResult.Deny("participant-not-assigned-to-team");
    }
}
