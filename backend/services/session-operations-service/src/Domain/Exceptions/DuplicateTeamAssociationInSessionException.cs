namespace umbral_backend.Domain.Exceptions;

public sealed class DuplicateTeamAssociationInSessionException : DomainException
{
    public DuplicateTeamAssociationInSessionException(Guid referenceTeamId)
        : base($"Team reference '{referenceTeamId}' is already associated to this live session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
