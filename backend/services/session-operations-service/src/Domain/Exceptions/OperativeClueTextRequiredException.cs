namespace umbral_backend.Domain.Exceptions;

public sealed class OperativeClueTextRequiredException : DomainException
{
    public OperativeClueTextRequiredException()
        : base("Operative clue text is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
