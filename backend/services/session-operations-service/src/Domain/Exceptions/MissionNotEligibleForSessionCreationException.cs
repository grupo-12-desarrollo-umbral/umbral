namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNotEligibleForSessionCreationException : DomainException
{
    public MissionNotEligibleForSessionCreationException(int missionId, string reason)
        : base($"Mission '{missionId}' cannot be used to create a new session because {reason}.")
    {
        MissionId = missionId;
        Reason = reason;
    }

    public int MissionId { get; }

    public string Reason { get; }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string ErrorCode => "mission-not-eligible-for-session";
}
