namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionRequiresAtLeastOneTeamException : DomainException
{
    public LiveSessionRequiresAtLeastOneTeamException()
        : base("A live session must have at least one associated team before it can become active.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string ErrorCode => "session-no-teams";
}
