using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class Team : BaseAuditableEntity
{
    private Team()
    {
        TeamId = Guid.Empty;
        DisplayName = string.Empty;
        TeamCode = string.Empty;
    }

    private Team(Guid teamId, string displayName, string teamCode)
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

    public static Team Register(string displayName, string teamCode)
    {
        var team = new Team(Guid.NewGuid(), displayName, teamCode);
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
