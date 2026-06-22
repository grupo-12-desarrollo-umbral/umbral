namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNodeTitleRequiredException : DomainException
{
    public MissionNodeTitleRequiredException()
        : base("Mission node title is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
