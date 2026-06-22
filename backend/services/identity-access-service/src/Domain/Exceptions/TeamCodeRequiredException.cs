namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCodeRequiredException : DomainException
{
    public TeamCodeRequiredException()
        : base("Team code is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
