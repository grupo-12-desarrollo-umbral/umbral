using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Teams.Commands.AuthorizeParticipantForTeam;
using umbral_backend.Application.Teams.Commands.DeactivateTeam;
using umbral_backend.Application.Teams.Commands.RegisterTeam;
using umbral_backend.Application.Teams.Commands.UpdateTeam;
using umbral_backend.Application.Teams.Common;
using umbral_backend.Application.Teams.Queries.GetTeamParticipants;
using umbral_backend.Application.Teams.Queries.GetTeamById;
using umbral_backend.Application.Teams.Queries.GetTeams;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/teams")]
public sealed class TeamsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RegisterTeamResponse>> RegisterTeamAsync(
        RegisterTeamRequest request,
        CancellationToken cancellationToken)
    {
        var teamId = await sender.Send(
            new RegisterTeamCommand(request.DisplayName, request.TeamCode),
            cancellationToken);

        return Created($"/api/teams/{teamId}", new RegisterTeamResponse(teamId));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TeamDto>>> GetTeamsAsync(
        [FromQuery] GetTeamsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetTeamsQuery(request.Page, request.PageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDto>> GetTeamByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateTeamAsync(
        Guid id,
        UpdateTeamRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateTeamCommand(id, request.DisplayName, request.TeamCode),
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}/status")]
    public async Task<ActionResult<TeamDto>> DeactivateTeamAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateTeamCommand(id), cancellationToken);

        var result = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/participants")]
    public async Task<ActionResult<AuthorizeParticipantForTeamResponse>> AuthorizeParticipantForTeamAsync(
        Guid id,
        AuthorizeParticipantForTeamRequest request,
        CancellationToken cancellationToken)
    {
        var membershipId = await sender.Send(
            new AuthorizeParticipantForTeamCommand(id, request.UserId),
            cancellationToken);

        return Created(
            $"/api/teams/{id}/participants/{membershipId}",
            new AuthorizeParticipantForTeamResponse(membershipId));
    }

    [HttpGet("{id:guid}/participants")]
    public async Task<ActionResult<IReadOnlyList<RegisteredTeamMembershipDto>>> GetTeamParticipantsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTeamParticipantsQuery(id), cancellationToken);
        return Ok(result);
    }

    public sealed record RegisterTeamRequest(string DisplayName, string TeamCode);

    public sealed record UpdateTeamRequest(string DisplayName, string TeamCode);

    public sealed record GetTeamsRequest(int Page = 1, int PageSize = 20);

    public sealed record RegisterTeamResponse(Guid TeamId);

    public sealed record AuthorizeParticipantForTeamRequest(int UserId);

    public sealed record AuthorizeParticipantForTeamResponse(Guid TeamMembershipId);
}
