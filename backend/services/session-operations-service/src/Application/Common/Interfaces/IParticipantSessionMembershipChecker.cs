using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

// Single source of truth for "is the authenticated caller an active member of this session team?".
// Resolves the current user, matches the participant by ExternalIdentityId and the team by
// ReferenceTeamId, and requires an active TeamMember. Shared by the membership-validation query and
// the participant read-projection handlers so the rule is implemented exactly once.
public interface IParticipantSessionMembershipChecker
{
    ParticipantSessionMembershipResult Check(LiveSession liveSession, Guid teamId);
}

public sealed record ParticipantSessionMembershipResult(bool IsAllowed, string ReasonCode)
{
    public static ParticipantSessionMembershipResult Allowed { get; } = new(true, "allowed");

    public static ParticipantSessionMembershipResult Deny(string reasonCode) => new(false, reasonCode);
}
