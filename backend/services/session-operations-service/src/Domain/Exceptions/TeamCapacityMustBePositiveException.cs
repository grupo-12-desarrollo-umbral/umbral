namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCapacityMustBePositiveException : DomainException
{
    public TeamCapacityMustBePositiveException()
        : base("Team capacity must be greater than zero.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
