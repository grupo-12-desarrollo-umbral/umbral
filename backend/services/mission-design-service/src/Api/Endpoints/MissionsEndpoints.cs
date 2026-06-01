using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;

namespace umbral_backend.Web.Endpoints;

public sealed class MissionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var missions = groupBuilder.MapGroup("/api/missions");

        missions.MapPost("/", CreateMission);
        missions.MapGet("/", GetMissionCatalog);
        missions.MapGet("/{id:int}", GetMissionDetail);
        missions.MapPut("/{id:int}", UpdateMission);
        missions.MapDelete("/{id:int}", DeactivateMission);
    }

    private static async Task<Created<MissionResponse>> CreateMission(
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

        return TypedResults.Created($"/api/missions/{mission.Id}", MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<IReadOnlyList<MissionSummaryResponse>>> GetMissionCatalog(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new GetMissionCatalogQuery(), cancellationToken);
        IReadOnlyList<MissionSummaryResponse> response = missions
            .Select(MissionSummaryResponse.FromDto)
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<MissionResponse>> GetMissionDetail(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetMissionDetailQuery(id), cancellationToken);
        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> UpdateMission(
        ISender sender,
        int id,
        UpdateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UpdateMissionCommand(
                id,
                request.Name,
                request.Description,
                request.Difficulty,
                request.MaximumTimeMinutes),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<NoContent> DeactivateMission(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateMissionCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }

    public sealed record CreateMissionRequest(
        string Name,
        string Description,
        string Difficulty,
        int MaximumTimeMinutes);

    public sealed record UpdateMissionRequest(
        string Name,
        string Description,
        string Difficulty,
        int MaximumTimeMinutes);

    public sealed record MissionResponse(
        int Id,
        string Name,
        string Description,
        string Difficulty,
        int MaximumTimeMinutes,
        bool IsActive,
        string ActivationState,
        bool IsSourceReady)
    {
        public static MissionResponse FromDto(MissionDto missionDto)
        {
            var isActive = !string.Equals(missionDto.Status, "Inactive", StringComparison.Ordinal);

            return new MissionResponse(
                missionDto.Id,
                missionDto.Name,
                missionDto.Description,
                missionDto.Difficulty,
                missionDto.MaximumTimeMinutes,
                isActive,
                missionDto.Status,
                string.Equals(missionDto.Status, "Ready", StringComparison.Ordinal));
        }
    }

    public sealed record MissionSummaryResponse(
        int Id,
        string Name,
        string Description,
        string Difficulty,
        bool IsActive,
        string ActivationState,
        bool IsSourceReady)
    {
        public static MissionSummaryResponse FromDto(MissionSummaryDto missionDto)
        {
            var isActive = !string.Equals(missionDto.Status, "Inactive", StringComparison.Ordinal);

            return new MissionSummaryResponse(
                missionDto.Id,
                missionDto.Name,
                missionDto.Description,
                missionDto.Difficulty,
                isActive,
                missionDto.Status,
                string.Equals(missionDto.Status, "Ready", StringComparison.Ordinal));
        }
    }
}
