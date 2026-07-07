namespace umbral_backend.Domain.Entities;

// Eligibility whitelist entry: the participant MAY join the registered team.
// It is not an assignment, a roster slot, or a lock — there is no "assigned at".
public sealed class RegisteredTeamMembership : BaseEntity
{
    private RegisteredTeamMembership()
    {
        TeamMembershipId = Guid.Empty;
        TeamId = Guid.Empty;
        UserId = 0;
    }

    private RegisteredTeamMembership(Guid teamMembershipId, Guid teamId, int userId)
    {
        TeamMembershipId = teamMembershipId;
        TeamId = teamId;
        UserId = userId;
    }

    public Guid TeamMembershipId { get; private set; }

    public Guid TeamId { get; private set; }

    public int UserId { get; private set; }

    internal static RegisteredTeamMembership Authorize(Guid teamId, int userId)
    {
        return new RegisteredTeamMembership(Guid.NewGuid(), teamId, userId);
    }
}
