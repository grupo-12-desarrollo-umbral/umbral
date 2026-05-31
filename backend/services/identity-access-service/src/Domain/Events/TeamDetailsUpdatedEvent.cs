namespace umbral_backend.Domain.Events;

public sealed class TeamDetailsUpdatedEvent : BaseEvent
{
    public TeamDetailsUpdatedEvent(Guid teamId, string displayName, string teamCode)
    {
        TeamId = teamId;
        DisplayName = displayName;
        TeamCode = teamCode;
    }

    public Guid TeamId { get; }

    public string DisplayName { get; }

    public string TeamCode { get; }
}
