namespace umbral_backend.Domain.Exceptions;

public sealed class TeamNotInAuthorizedSetException : DomainException
{
    public TeamNotInAuthorizedSetException(Guid teamId)
        : base($"Team '{teamId}' is not in the participant's authorized set for this session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;
}
