using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;
using umbral_backend.Application.Teams.Commands.DeactivateTeam;
using umbral_backend.Application.Teams.Commands.RegisterTeam;
using umbral_backend.Application.Teams.Commands.UpdateTeam;
using umbral_backend.Application.Teams.DTOs;
using umbral_backend.Application.Teams.Queries.GetTeamById;
using umbral_backend.Application.Teams.Queries.GetTeamParticipants;
using umbral_backend.Application.Teams.Queries.GetTeams;

namespace umbral_backend.Api.Endpoints;

public sealed class TeamsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var teams = groupBuilder.MapGroup("/api/teams");

        teams.MapPost(string.Empty, RegisterTeamAsync);
        teams.MapGet(string.Empty, GetTeamsAsync);
        teams.MapGet("/{id:guid}", GetTeamByIdAsync);
        teams.MapPatch("/{id:guid}", UpdateTeamAsync);
        teams.MapDelete("/{id:guid}/status", DeactivateTeamAsync);
        teams.MapPost("/{id:guid}/participants", AssignParticipantToTeamAsync);
        teams.MapGet("/{id:guid}/participants", GetTeamParticipantsAsync);
    }

    private static async Task<Created<RegisterTeamResponse>> RegisterTeamAsync(
        RegisterTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var teamId = await sender.Send(
            new RegisterTeamCommand(request.DisplayName, request.TeamCode),
            cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}", new RegisterTeamResponse(teamId));
    }

    private static async Task<Ok<PagedResult<TeamDto>>> GetTeamsAsync(
        ISender sender,
        [AsParameters] GetTeamsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetTeamsQuery(request.Page, request.PageSize),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<TeamDto>> GetTeamByIdAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> UpdateTeamAsync(
        Guid id,
        UpdateTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateTeamCommand(id, request.DisplayName, request.TeamCode),
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<TeamDto>> DeactivateTeamAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateTeamCommand(id), cancellationToken);

        var result = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Created<AssignParticipantToTeamResponse>> AssignParticipantToTeamAsync(
        Guid id,
        AssignParticipantToTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var membershipId = await sender.Send(
            new AssignParticipantToTeamCommand(id, request.UserId),
            cancellationToken);

        return TypedResults.Created(
            $"/api/teams/{id}/participants/{membershipId}",
            new AssignParticipantToTeamResponse(membershipId));
    }

    private static async Task<Ok<IReadOnlyList<TeamMembershipDto>>> GetTeamParticipantsAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTeamParticipantsQuery(id), cancellationToken);
        return TypedResults.Ok(result);
    }

    public sealed record RegisterTeamRequest(string DisplayName, string TeamCode);

    public sealed record UpdateTeamRequest(string DisplayName, string TeamCode);

    public sealed record GetTeamsRequest(int Page = 1, int PageSize = 20);

    public sealed record RegisterTeamResponse(Guid TeamId);

    public sealed record AssignParticipantToTeamRequest(int UserId);

    public sealed record AssignParticipantToTeamResponse(Guid TeamMembershipId);
}
