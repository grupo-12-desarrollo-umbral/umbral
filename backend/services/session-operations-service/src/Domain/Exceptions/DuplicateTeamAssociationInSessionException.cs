namespace umbral_backend.Domain.Exceptions;

public sealed class DuplicateTeamAssociationInSessionException : Exception
{
    public DuplicateTeamAssociationInSessionException(Guid referenceTeamId)
        : base($"Team reference '{referenceTeamId}' is already associated to the session.")
    {
    }
}
