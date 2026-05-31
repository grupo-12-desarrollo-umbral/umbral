namespace umbral_backend.Domain.Exceptions;

public sealed class UserEmailRequiredException : Exception
{
    public UserEmailRequiredException()
        : base("User email is required.")
    {
    }
}
