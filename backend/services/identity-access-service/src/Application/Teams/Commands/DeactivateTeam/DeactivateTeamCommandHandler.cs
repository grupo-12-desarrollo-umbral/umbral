using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.DeactivateTeam;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.DeactivateTeam;

public sealed class DeactivateTeamCommandHandler : IRequestHandler<DeactivateTeamCommand>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public DeactivateTeamCommandHandler(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeactivateTeamCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), request.TeamId);

        team.Deactivate();

        await _teamRepository.UpdateAsync(team, cancellationToken);
    }
}
