namespace umbral_backend.Domain.Exceptions;

public sealed class TeamIdentityRequiredException : DomainException
{
    public TeamIdentityRequiredException()
        : base("Team identity is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
