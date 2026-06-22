namespace umbral_backend.Domain.Exceptions;

public sealed class ReferenceTeamIdRequiredException : DomainException
{
    public ReferenceTeamIdRequiredException()
        : base("Reference team id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
