using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Common;
using umbral_backend.Application.Teams.Queries.GetTeamById;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Queries.GetTeamById;

public sealed class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public GetTeamByIdQueryHandler(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task<TeamDto> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegisteredTeam), request.TeamId);

        return new TeamDto(
            team.TeamId,
            team.DisplayName,
            team.TeamCode,
            team.IsActive,
            team.Created,
            team.LastModified);
    }
}
