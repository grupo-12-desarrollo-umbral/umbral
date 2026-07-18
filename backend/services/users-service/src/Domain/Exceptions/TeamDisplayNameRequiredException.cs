namespace umbral_backend.Domain.Exceptions;

public sealed class TeamDisplayNameRequiredException : DomainException
{
    public TeamDisplayNameRequiredException()
        : base("Team display name is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
