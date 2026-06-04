namespace umbral_backend.Domain.Events;

public sealed class TeamRegisteredInSessionEvent : BaseEvent
{
    public TeamRegisteredInSessionEvent(
        Guid liveSessionId,
        Guid teamId,
        string teamCode,
        Guid? referenceTeamId = null)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        TeamCode = teamCode;
        ReferenceTeamId = referenceTeamId;
    }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public string TeamCode { get; }

    public Guid? ReferenceTeamId { get; }
}
