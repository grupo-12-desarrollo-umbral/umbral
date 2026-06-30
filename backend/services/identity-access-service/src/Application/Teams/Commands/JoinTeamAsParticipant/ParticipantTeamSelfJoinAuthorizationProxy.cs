using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class ParticipantTeamSelfJoinAuthorizationProxy : IRequestHandler<JoinTeamAsParticipantCommand, Guid>
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly JoinTeamAsParticipantCommandHandler _inner;

    public ParticipantTeamSelfJoinAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        JoinTeamAsParticipantCommandHandler inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<Guid> Handle(JoinTeamAsParticipantCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.ParticipantExperience);

        return await _inner.Handle(request, cancellationToken);
    }
}
