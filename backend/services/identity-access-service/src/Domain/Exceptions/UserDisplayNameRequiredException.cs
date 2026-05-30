namespace umbral_backend.Domain.Exceptions;

public sealed class UserDisplayNameRequiredException : Exception
{
    public UserDisplayNameRequiredException()
        : base("User display name is required.")
    {
    }
}
