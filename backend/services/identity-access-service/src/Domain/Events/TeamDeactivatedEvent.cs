namespace umbral_backend.Domain.Events;

public sealed class TeamDeactivatedEvent : BaseEvent
{
    public TeamDeactivatedEvent(Guid teamId)
    {
        TeamId = teamId;
    }

    public Guid TeamId { get; }
}
