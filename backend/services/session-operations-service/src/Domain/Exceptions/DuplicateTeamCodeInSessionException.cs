namespace umbral_backend.Domain.Exceptions;

public sealed class DuplicateTeamCodeInSessionException : Exception
{
    public DuplicateTeamCodeInSessionException(string teamCode)
        : base($"Team code '{teamCode}' is already registered in the session.")
    {
    }
}
