namespace umbral_backend.Domain.Entities;

public sealed class TeamMembership : BaseEntity
{
    private TeamMembership()
    {
        TeamMembershipId = Guid.Empty;
        TeamId = Guid.Empty;
        UserId = 0;
    }

    private TeamMembership(Guid teamMembershipId, Guid teamId, int userId, DateTimeOffset assignedAt)
    {
        TeamMembershipId = teamMembershipId;
        TeamId = teamId;
        UserId = userId;
        AssignedAt = assignedAt;
    }

    public Guid TeamMembershipId { get; private set; }

    public Guid TeamId { get; private set; }

    public int UserId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    internal static TeamMembership Assign(Guid teamId, int userId, DateTimeOffset assignedAt)
    {
        return new TeamMembership(Guid.NewGuid(), teamId, userId, assignedAt);
    }
}
