using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Teams.Common;
using umbral_backend.Application.Teams.Queries.GetTeams;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Queries.GetTeams;

public sealed class GetTeamsQueryHandler : IRequestHandler<GetTeamsQuery, PagedResult<TeamDto>>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public GetTeamsQueryHandler(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task<PagedResult<TeamDto>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        var teams = await _teamRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        return new PagedResult<TeamDto>
        {
            Items = teams.Items.Select(Map).ToArray(),
            TotalCount = teams.TotalCount,
            Page = teams.Page,
            PageSize = teams.PageSize
        };
    }

    private static TeamDto Map(Team team)
    {
        return new TeamDto(
            team.TeamId,
            team.DisplayName,
            team.TeamCode,
            team.IsActive,
            team.Created,
            team.LastModified);
    }
}
