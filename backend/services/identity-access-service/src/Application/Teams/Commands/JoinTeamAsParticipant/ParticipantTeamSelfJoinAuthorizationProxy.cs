using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class ParticipantTeamSelfJoinAuthorizationProxy : IJoinTeamAsParticipantService
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IJoinTeamAsParticipantService _inner;

    public ParticipantTeamSelfJoinAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        IJoinTeamAsParticipantService inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<Guid> JoinAsync(JoinTeamAsParticipantCommand command, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.ParticipantExperience);

        return await _inner.JoinAsync(command, cancellationToken);
    }
}
