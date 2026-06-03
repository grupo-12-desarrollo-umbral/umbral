using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class Team : BaseEntity
{
    private readonly List<TeamMember> _members = new();

    private Team()
    {
        TeamId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamCode = null!;
        DisplayName = string.Empty;
    }

    private Team(Guid liveSessionId, string displayName, TeamCode teamCode)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new TeamDisplayNameRequiredException();
        }

        TeamId = Guid.NewGuid();
        LiveSessionId = liveSessionId;
        TeamCode = teamCode;
        DisplayName = displayName.Trim();
        JoinStatus = TeamJoinStatus.Open;
    }

    public Guid TeamId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public TeamCode TeamCode { get; private set; }

    public string DisplayName { get; private set; }

    public int? CurrentScore { get; private set; }

    public Guid? CurrentProgressNodeId { get; private set; }

    public Guid? CurrentClueNodeId { get; private set; }

    public int ReleasedClueCount { get; private set; }

    public DateTimeOffset? LastScoreCalculatedAt { get; private set; }

    public TeamJoinStatus JoinStatus { get; private set; }

    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    public int ActiveMemberCount => _members.Count(member => member.IsActive);

    public static Team Register(Guid liveSessionId, string displayName, string teamCode)
    {
        return new Team(liveSessionId, displayName, TeamCode.Create(teamCode));
    }

    public void LockNewParticipants()
    {
        JoinStatus = TeamJoinStatus.Locked;
    }

    public void ReopenForNewParticipants()
    {
        JoinStatus = TeamJoinStatus.Open;
    }

    public void CloseNewParticipants()
    {
        JoinStatus = TeamJoinStatus.Closed;
    }

    internal TeamMember AssignParticipant(SessionParticipant participant, DateTimeOffset occurredAt)
    {
        var existingMember = _members.SingleOrDefault(member =>
            member.SessionParticipantId == participant.SessionParticipantId &&
            member.IsActive);

        if (existingMember is not null)
        {
            return existingMember;
        }

        var member = TeamMember.Assign(TeamId, participant.SessionParticipantId, occurredAt);
        _members.Add(member);
        return member;
    }
}
