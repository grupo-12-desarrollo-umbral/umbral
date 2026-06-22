namespace umbral_backend.Domain.Exceptions;

public sealed class DifficultyValueRequiredException : DomainException
{
    public DifficultyValueRequiredException()
        : base("Mission difficulty is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
