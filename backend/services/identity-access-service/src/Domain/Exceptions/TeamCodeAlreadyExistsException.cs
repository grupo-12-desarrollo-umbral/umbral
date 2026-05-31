namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCodeAlreadyExistsException : Exception
{
    public TeamCodeAlreadyExistsException(string teamCode)
        : base($"Team code '{teamCode}' already exists.")
    {
    }
}
