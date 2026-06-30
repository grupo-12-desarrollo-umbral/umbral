using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.RegisterTeam;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.RegisterTeam;

public sealed class RegisterTeamCommandHandler : IRequestHandler<RegisterTeamCommand, Guid>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public RegisterTeamCommandHandler(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task<Guid> Handle(RegisterTeamCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        EnsureActorCanManageTeams(actor);

        var normalizedTeamCode = request.TeamCode.Trim();

        if (await _teamRepository.TeamCodeExistsAsync(normalizedTeamCode, excludeTeamId: null, cancellationToken))
        {
            throw new TeamCodeAlreadyExistsException(normalizedTeamCode);
        }

        var team = Team.Register(request.DisplayName, request.TeamCode);

        await _teamRepository.AddAsync(team, cancellationToken);

        return team.TeamId;
    }

    private void EnsureActorCanManageTeams(User actor)
    {
        var decision = _accessPolicy.Evaluate(actor, ProtectedCapability.OperatorPanel);

        if (!actor.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(actor.Id);
        }

        if (!decision.IsAllowed)
        {
            throw new UserRoleNotAuthorizedException(actor.Role, ProtectedCapability.OperatorPanel);
        }
    }
}
