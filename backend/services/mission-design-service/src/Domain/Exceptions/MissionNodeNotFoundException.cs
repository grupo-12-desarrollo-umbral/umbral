namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNodeNotFoundException : DomainException
{
    public MissionNodeNotFoundException(int missionNodeId)
        : base($"Mission node '{missionNodeId}' was not found in the mission.")
    {
        MissionNodeId = missionNodeId;
    }

    public int MissionNodeId { get; }

    public override ErrorCategory Category => ErrorCategory.NotFound;
}
