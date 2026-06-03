using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class LiveSessionReference : BaseAuditableEntity
{
    private const int SessionCodeLength = 6;
    private readonly List<SessionTeamAssociation> _teamAssociations = new();

    private LiveSessionReference()
    {
        LiveSessionId = Guid.Empty;
        SessionCode = string.Empty;
    }

    private LiveSessionReference(Guid liveSessionId, string sessionCode)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new LiveSessionReferenceIdRequiredException();
        }

        LiveSessionId = liveSessionId;
        SessionCode = RequireSessionCode(sessionCode);
    }

    public Guid LiveSessionId { get; private set; }

    public string SessionCode { get; private set; }

    public IReadOnlyCollection<SessionTeamAssociation> TeamAssociations => _teamAssociations.AsReadOnly();

    public static LiveSessionReference Create(Guid liveSessionId, string sessionCode)
        => new(liveSessionId, sessionCode);

    public SessionTeamAssociation AssociateTeam(Guid teamId)
    {
        if (_teamAssociations.Any(association => association.TeamId == teamId))
        {
            throw new TeamAlreadyAssociatedWithSessionException(LiveSessionId, teamId);
        }

        var association = SessionTeamAssociation.Create(LiveSessionId, teamId);
        _teamAssociations.Add(association);

        return association;
    }

    private static string RequireSessionCode(string sessionCode)
    {
        if (string.IsNullOrWhiteSpace(sessionCode))
        {
            throw new SessionCodeRequiredException();
        }

        var normalizedCode = sessionCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length != SessionCodeLength || !normalizedCode.All(char.IsLetterOrDigit))
        {
            throw new SessionCodeFormatInvalidException(SessionCodeLength);
        }

        return normalizedCode;
    }
}
