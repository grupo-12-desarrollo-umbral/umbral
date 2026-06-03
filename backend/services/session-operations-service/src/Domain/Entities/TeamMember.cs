using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

public sealed class TeamMember : BaseEntity
{
    private TeamMember()
    {
        TeamMemberId = Guid.Empty;
        TeamId = Guid.Empty;
        SessionParticipantId = Guid.Empty;
    }

    private TeamMember(Guid teamId, Guid sessionParticipantId, DateTimeOffset joinedAt)
    {
        TeamMemberId = Guid.NewGuid();
        TeamId = teamId;
        SessionParticipantId = sessionParticipantId;
        MembershipStatus = TeamMembershipStatus.Active;
        JoinedAt = joinedAt;
    }

    public Guid TeamMemberId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid SessionParticipantId { get; private set; }

    public TeamMembershipStatus MembershipStatus { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? LeftAt { get; private set; }

    public bool IsActive => MembershipStatus == TeamMembershipStatus.Active;

    public static TeamMember Assign(Guid teamId, Guid sessionParticipantId, DateTimeOffset joinedAt)
    {
        return new TeamMember(teamId, sessionParticipantId, joinedAt);
    }

    public void Remove(DateTimeOffset leftAt)
    {
        MembershipStatus = TeamMembershipStatus.Removed;
        LeftAt = leftAt;
    }
}
