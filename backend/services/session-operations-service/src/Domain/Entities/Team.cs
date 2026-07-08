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
        ReferenceTeamId = null;
        TeamCode = null!;
        DisplayName = string.Empty;
    }

    private Team(
        Guid liveSessionId,
        Guid teamId,
        Guid? referenceTeamId,
        string displayName,
        TeamCode teamCode,
        int capacity)
    {
        if (teamId == Guid.Empty)
        {
            throw new TeamIdentityRequiredException();
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new TeamDisplayNameRequiredException();
        }

        if (capacity <= 0)
        {
            throw new TeamCapacityMustBePositiveException();
        }

        TeamId = teamId;
        LiveSessionId = liveSessionId;
        ReferenceTeamId = referenceTeamId;
        TeamCode = teamCode;
        DisplayName = displayName.Trim();
        Capacity = capacity;
        JoinStatus = TeamJoinStatus.Open;
    }

    public Guid TeamId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid? ReferenceTeamId { get; private set; }

    public TeamCode TeamCode { get; private set; }

    public string DisplayName { get; private set; }

    public int Capacity { get; private set; }

    public int? CurrentScore { get; private set; }

    public Guid? CurrentProgressNodeId { get; private set; }

    public Guid? CurrentClueNodeId { get; private set; }

    public int ReleasedClueCount { get; private set; }

    public DateTimeOffset? LastScoreCalculatedAt { get; private set; }

    public TeamJoinStatus JoinStatus { get; private set; }

    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    public int ActiveMemberCount => _members.Count(member => member.IsActive);

    public static Team Register(Guid liveSessionId, string displayName, string teamCode, int capacity)
    {
        return Register(liveSessionId, Guid.NewGuid(), displayName, teamCode, capacity);
    }

    public static Team Register(Guid liveSessionId, Guid teamId, string displayName, string teamCode, int capacity)
    {
        return new Team(liveSessionId, teamId, null, displayName, TeamCode.Create(teamCode), capacity);
    }

    public static Team Associate(
        Guid liveSessionId,
        Guid referenceTeamId,
        string displayName,
        string teamCode,
        int capacity)
    {
        if (referenceTeamId == Guid.Empty)
        {
            throw new ReferenceTeamIdRequiredException();
        }

        return new Team(
            liveSessionId,
            Guid.NewGuid(),
            referenceTeamId,
            displayName,
            TeamCode.Create(teamCode),
            capacity);
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

    // Releases the participant's active membership, freeing a capacity slot. Used when a participant
    // switches teams pre-start (#89, decisions §8: "Switching out frees a slot"). No-op if they hold
    // no active membership here.
    internal void ReleaseParticipant(Guid sessionParticipantId, DateTimeOffset occurredAt)
    {
        var member = _members.SingleOrDefault(member =>
            member.SessionParticipantId == sessionParticipantId &&
            member.IsActive);

        member?.Remove(occurredAt);
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
