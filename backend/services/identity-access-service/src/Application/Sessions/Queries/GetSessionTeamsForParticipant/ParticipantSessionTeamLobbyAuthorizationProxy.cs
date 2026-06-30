using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed class ParticipantSessionTeamLobbyAuthorizationProxy : IGetSessionTeamsForParticipantService
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IGetSessionTeamsForParticipantService _inner;

    public ParticipantSessionTeamLobbyAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        IGetSessionTeamsForParticipantService inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<SessionTeamLobbyDto> GetAsync(
        GetSessionTeamsForParticipantQuery query,
        CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.ParticipantExperience);

        return await _inner.GetAsync(query, cancellationToken);
    }
}
