namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionRequiresAtLeastOneTeamException : Exception
{
    public LiveSessionRequiresAtLeastOneTeamException()
        : base("A live session must have at least one team before it can become active.")
    {
    }
}
