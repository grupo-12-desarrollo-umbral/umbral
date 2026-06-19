namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNodeNotFoundException : Exception
{
    public MissionNodeNotFoundException(int missionNodeId)
        : base($"Mission node '{missionNodeId}' was not found in the mission.")
    {
        MissionNodeId = missionNodeId;
    }

    public int MissionNodeId { get; }
}
