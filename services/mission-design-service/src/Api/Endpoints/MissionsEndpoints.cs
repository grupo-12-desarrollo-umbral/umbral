using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissionById;
using umbral_backend.Application.Missions.Queries.GetMissions;

namespace umbral_backend.Web.Endpoints;

public sealed class MissionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var missions = groupBuilder.MapGroup("/api/missions");

        missions.MapPost("/", CreateMission);
        missions.MapGet("/", GetMissions);
        missions.MapGet("/{id:int}", GetMissionById);
    }

    private static async Task<Created<MissionDto>> CreateMission(
        ISender sender,
        CreateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new CreateMissionCommand(
                request.Name,
                request.Description,
                request.Difficulty,
                request.MaximumTimeMinutes),
            cancellationToken);

        return TypedResults.Created($"/api/missions/{mission.Id}", mission);
    }

    private static async Task<Ok<IReadOnlyList<MissionSummaryDto>>> GetMissions(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new GetMissionsQuery(), cancellationToken);
        return TypedResults.Ok(missions);
    }

    private static async Task<Ok<MissionDto>> GetMissionById(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetMissionByIdQuery(id), cancellationToken);
        return TypedResults.Ok(mission);
    }

    public sealed record CreateMissionRequest(
        string Name,
        string Description,
        string Difficulty,
        int MaximumTimeMinutes);
}
