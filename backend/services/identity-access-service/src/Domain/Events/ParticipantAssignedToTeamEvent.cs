namespace umbral_backend.Domain.Events;

public sealed class ParticipantAssignedToTeamEvent : BaseEvent
{
    public ParticipantAssignedToTeamEvent(Guid teamId, int userId)
    {
        TeamId = teamId;
        UserId = userId;
    }

    public Guid TeamId { get; }

    public int UserId { get; }
}
