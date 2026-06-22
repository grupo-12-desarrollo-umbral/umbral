namespace umbral_backend.Domain.Exceptions;

public sealed class ScoreValueMustBePositiveException : DomainException
{
    public ScoreValueMustBePositiveException()
        : base("Score value must be a positive number of points.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
