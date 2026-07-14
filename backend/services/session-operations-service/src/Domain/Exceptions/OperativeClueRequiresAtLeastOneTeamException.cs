namespace umbral_backend.Domain.Exceptions;

public sealed class OperativeClueRequiresAtLeastOneTeamException : DomainException
{
    public OperativeClueRequiresAtLeastOneTeamException()
        : base("An operative clue must be assigned to at least one team.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
