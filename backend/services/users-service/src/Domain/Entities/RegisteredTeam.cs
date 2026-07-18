using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

// Users-owned team catalog entry (reference data). Memberships form a may-join
// eligibility whitelist for Participants — never a live assignment or lock.
public sealed class RegisteredTeam : BaseAuditableEntity
{
    private readonly List<RegisteredTeamMembership> _memberships = new();

    private RegisteredTeam()
    {
        TeamId = Guid.Empty;
        DisplayName = string.Empty;
        TeamCode = string.Empty;
    }

    private RegisteredTeam(Guid teamId, string displayName, string teamCode)
    {
        TeamId = teamId;
        DisplayName = RequireDisplayName(displayName);
        TeamCode = RequireTeamCode(teamCode);
        IsActive = true;
    }

    public Guid TeamId { get; private set; }

    public string DisplayName { get; private set; }

    public string TeamCode { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<RegisteredTeamMembership> Memberships => _memberships.AsReadOnly();

    public static RegisteredTeam Register(string displayName, string teamCode)
    {
        var team = new RegisteredTeam(Guid.NewGuid(), displayName, teamCode);
        team.AddDomainEvent(new TeamRegisteredEvent(team.TeamId, team.DisplayName, team.TeamCode));

        return team;
    }

    public void UpdateDetails(string displayName, string teamCode)
    {
        DisplayName = RequireDisplayName(displayName);
        TeamCode = RequireTeamCode(teamCode);
        AddDomainEvent(new TeamDetailsUpdatedEvent(TeamId, DisplayName, TeamCode));
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new TeamAlreadyDeactivatedException(TeamId);
        }

        IsActive = false;
        AddDomainEvent(new TeamDeactivatedEvent(TeamId));
    }

    // Adds the participant to this team's eligibility whitelist (may-join authorization).
    // It does not assign, seat, or lock the participant to the team.
    public RegisteredTeamMembership AuthorizeParticipant(int userId)
    {
        if (!IsActive)
        {
            throw new TeamNotActiveException(TeamId);
        }

        if (_memberships.Any(membership => membership.UserId == userId))
        {
            throw new ParticipantAlreadyAuthorizedForTeamException(TeamId, userId);
        }

        var membership = RegisteredTeamMembership.Authorize(TeamId, userId);
        _memberships.Add(membership);

        return membership;
    }

    private static string RequireDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new TeamDisplayNameRequiredException();
        }

        return displayName.Trim();
    }

    private static string RequireTeamCode(string teamCode)
    {
        if (string.IsNullOrWhiteSpace(teamCode))
        {
            throw new TeamCodeRequiredException();
        }

        return teamCode.Trim();
    }
}
