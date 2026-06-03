namespace umbral_backend.Domain.Events;

public sealed class TeamRegisteredInSessionEvent : BaseEvent
{
    public TeamRegisteredInSessionEvent(Guid liveSessionId, Guid teamId, string teamCode)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        TeamCode = teamCode;
    }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public string TeamCode { get; }
}
