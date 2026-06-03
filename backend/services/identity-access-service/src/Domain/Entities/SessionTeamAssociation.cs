using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class SessionTeamAssociation : BaseEntity
{
    private SessionTeamAssociation()
    {
        SessionTeamAssociationId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
    }

    private SessionTeamAssociation(Guid sessionTeamAssociationId, Guid liveSessionId, Guid teamId)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new LiveSessionReferenceIdRequiredException();
        }

        if (teamId == Guid.Empty)
        {
            throw new SessionTeamAssociationTeamRequiredException();
        }

        SessionTeamAssociationId = sessionTeamAssociationId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
    }

    public Guid SessionTeamAssociationId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    internal static SessionTeamAssociation Create(Guid liveSessionId, Guid teamId)
        => new(Guid.NewGuid(), liveSessionId, teamId);
}
