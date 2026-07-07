using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.UpdateTeam;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.UpdateTeam;

public sealed class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public UpdateTeamCommandHandler(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegisteredTeam), request.TeamId);

        var normalizedTeamCode = request.TeamCode.Trim();

        if (await _teamRepository.TeamCodeExistsAsync(normalizedTeamCode, request.TeamId, cancellationToken))
        {
            throw new TeamCodeAlreadyExistsException(normalizedTeamCode);
        }

        team.UpdateDetails(request.DisplayName, request.TeamCode);

        await _teamRepository.UpdateAsync(team, cancellationToken);
    }
}
