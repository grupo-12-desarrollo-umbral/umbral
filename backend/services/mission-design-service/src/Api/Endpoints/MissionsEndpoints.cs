using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.SetTriviaQuizSelection;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;

namespace umbral_backend.Web.Endpoints;

public sealed class MissionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var missions = groupBuilder.MapGroup("/api/missions");

        missions.MapGet("/difficulties", GetDifficultyCatalog);
        missions.MapPost("/", CreateMission);
        missions.MapGet("/", GetMissionCatalog);
        missions.MapGet("/{id:int}", GetMissionDetail);
        missions.MapGet("/{id:int}/runtime-plan", GetMissionRuntimePlan);
        missions.MapPut("/{id:int}", UpdateMission);
        missions.MapDelete("/{id:int}", DeactivateMission);
        missions.MapPost("/{id:int}/activate", ActivateMission);
        missions.MapGet("/{id:int}/readiness", GetMissionReadiness);
        missions.MapPost("/{missionId:int}/nodes", AddMissionNode);
        missions.MapPut("/{missionId:int}/nodes/{nodeId:int}", UpdateMissionNode);
        missions.MapDelete("/{missionId:int}/nodes/{nodeId:int}", RemoveMissionNode);
        missions.MapPut(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/play-mode",
            AssignSubstagePlayMode);
        missions.MapPost(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets",
            AddTarget);
        missions.MapPut(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}",
            UpdateTarget);
        missions.MapDelete(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}",
            RemoveTarget);
        missions.MapPost(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}/clue-association",
            AssociateClueWithTarget);
        missions.MapDelete(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}/clue-association",
            UnassociateClueFromTarget);
        missions.MapPost(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/trivia-quiz-selection",
            SetTriviaQuizSelection);
        missions.MapPut(
            "/{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/trivia-quiz-selection",
            UpdateTriviaQuizSelection);
    }

    private static async Task<Ok<IReadOnlyList<DifficultyResponse>>> GetDifficultyCatalog(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var difficulties = await sender.Send(new GetDifficultyCatalogQuery(), cancellationToken);
        var response = difficulties
            .Select(DifficultyResponse.FromDto)
            .ToList();

        return TypedResults.Ok<IReadOnlyList<DifficultyResponse>>(response);
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

    private static async Task<Ok<MissionRuntimePlanResponse>> GetMissionRuntimePlan(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var missionRuntimePlan = await sender.Send(new GetMissionRuntimePlanQuery(id), cancellationToken);
        return TypedResults.Ok(MissionRuntimePlanResponse.FromDto(missionRuntimePlan));
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

    private static async Task<Ok<MissionResponse>> ActivateMission(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new ActivateMissionCommand(id), cancellationToken);
        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionReadinessResponse>> GetMissionReadiness(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var readiness = await sender.Send(new GetMissionReadinessQuery(id), cancellationToken);
        return TypedResults.Ok(MissionReadinessResponse.FromDto(readiness));
    }

    private static async Task<Ok<MissionResponse>> AddMissionNode(
        ISender sender,
        int missionId,
        AddMissionNodeRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new AddMissionNodeCommand(
                missionId,
                request.NodeType,
                request.Title,
                request.SequenceOrder,
                request.StageId,
                request.SubstageId,
                request.PlayMode,
                request.ClueText,
                request.ClueVisibilityPolicy),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> UpdateMissionNode(
        ISender sender,
        int missionId,
        int nodeId,
        UpdateMissionNodeRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UpdateMissionNodeCommand(
                missionId,
                nodeId,
                request.Title,
                request.SequenceOrder,
                request.ClueText,
                request.ClueVisibilityPolicy),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> RemoveMissionNode(
        ISender sender,
        int missionId,
        int nodeId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new RemoveMissionNodeCommand(missionId, nodeId), cancellationToken);
        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> AssignSubstagePlayMode(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        AssignSubstagePlayModeRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new AssignSubstagePlayModeCommand(missionId, stageId, substageId, request.PlayMode),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> AddTarget(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        AddTargetRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new AddTargetCommand(
                missionId,
                stageId,
                substageId,
                request.Name,
                request.QrCode,
                request.SequenceOrder,
                request.IsActive,
                request.WinnerScore),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> UpdateTarget(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        int targetId,
        UpdateTargetRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UpdateTargetCommand(
                missionId,
                stageId,
                substageId,
                targetId,
                request.Name,
                request.QrCode,
                request.SequenceOrder,
                request.IsActive,
                request.WinnerScore),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> RemoveTarget(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        int targetId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new RemoveTargetCommand(missionId, stageId, substageId, targetId),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> AssociateClueWithTarget(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        int targetId,
        AssociateClueWithTargetRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new AssociateClueWithTargetCommand(
                missionId,
                stageId,
                substageId,
                targetId,
                request.ClueId),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> UnassociateClueFromTarget(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        int targetId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UnassociateClueFromTargetCommand(missionId, stageId, substageId, targetId),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> SetTriviaQuizSelection(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        TriviaQuizSelectionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new SetTriviaQuizSelectionCommand(missionId, stageId, substageId, request.TriviaQuizId),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
    }

    private static async Task<Ok<MissionResponse>> UpdateTriviaQuizSelection(
        ISender sender,
        int missionId,
        int stageId,
        int substageId,
        TriviaQuizSelectionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UpdateTriviaQuizSelectionCommand(missionId, stageId, substageId, request.TriviaQuizId),
            cancellationToken);

        return TypedResults.Ok(MissionResponse.FromDto(mission));
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

    public sealed record AddMissionNodeRequest(
        string NodeType,
        string Title,
        int SequenceOrder,
        int? StageId = null,
        int? SubstageId = null,
        string? PlayMode = null,
        string? ClueText = null,
        string? ClueVisibilityPolicy = null);

    public sealed record UpdateMissionNodeRequest(
        string Title,
        int SequenceOrder,
        string? ClueText = null,
        string? ClueVisibilityPolicy = null);

    public sealed record AssignSubstagePlayModeRequest(string PlayMode);

    public sealed record AddTargetRequest(
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive = true,
        int? WinnerScore = null);

    public sealed record UpdateTargetRequest(
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive,
        int? WinnerScore = null);

    public sealed record AssociateClueWithTargetRequest(int ClueId);

    public sealed record TriviaQuizSelectionRequest(int TriviaQuizId);

    public sealed record MissionResponse(
        int Id,
        string Name,
        string Description,
        string Difficulty,
        int MaximumTimeMinutes,
        bool IsActive,
        string ActivationState,
        bool IsSourceReady,
        IReadOnlyList<MissionStageResponse> Stages)
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
                string.Equals(missionDto.Status, "Ready", StringComparison.Ordinal),
                missionDto.Stages?.Select(MissionStageResponse.FromDto).ToList() ?? []);
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

    public sealed record DifficultyResponse(string Value)
    {
        public static DifficultyResponse FromDto(DifficultyDto dto) => new(dto.Value);
    }

    public sealed record MissionStageResponse(
        int Id,
        string Title,
        int SequenceOrder,
        IReadOnlyList<MissionSubstageResponse> Substages)
    {
        public static MissionStageResponse FromDto(MissionStageDto dto)
        {
            return new MissionStageResponse(
                dto.Id,
                dto.Title,
                dto.SequenceOrder,
                dto.Substages?.Select(MissionSubstageResponse.FromDto).ToList() ?? []);
        }
    }

    public sealed record MissionSubstageResponse(
        int Id,
        string Title,
        int SequenceOrder,
        string PlayMode,
        int? WinnerScore,
        TriviaQuizSelectionResponse? TriviaQuizSelection,
        IReadOnlyList<MissionTargetResponse> Targets,
        IReadOnlyList<MissionClueResponse> Clues)
    {
        public static MissionSubstageResponse FromDto(MissionSubstageDto dto)
        {
            return new MissionSubstageResponse(
                dto.Id,
                dto.Title,
                dto.SequenceOrder,
                dto.PlayMode,
                dto.WinnerScore,
                dto.TriviaQuizSelection is null ? null : TriviaQuizSelectionResponse.FromDto(dto.TriviaQuizSelection),
                dto.Targets?.Select(MissionTargetResponse.FromDto).ToList() ?? [],
                dto.Clues?.Select(MissionClueResponse.FromDto).ToList() ?? []);
        }
    }

    public sealed record MissionTargetResponse(
        int Id,
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive,
        int? ClueId)
    {
        public static MissionTargetResponse FromDto(MissionTargetDto dto)
        {
            return new MissionTargetResponse(
                dto.Id,
                dto.Name,
                dto.QrCode,
                dto.SequenceOrder,
                dto.IsActive,
                dto.ClueId);
        }
    }

    public sealed record MissionClueResponse(
        int Id,
        string Title,
        int SequenceOrder,
        string Text,
        string VisibilityPolicy)
    {
        public static MissionClueResponse FromDto(MissionClueDto dto)
        {
            return new MissionClueResponse(
                dto.Id,
                dto.Title,
                dto.SequenceOrder,
                dto.Text,
                dto.VisibilityPolicy);
        }
    }

    public sealed record TriviaQuizSelectionResponse(int TriviaQuizId)
    {
        public static TriviaQuizSelectionResponse FromDto(TriviaQuizSelectionDto dto) => new(dto.TriviaQuizId);
    }

    public sealed record MissionReadinessResponse(
        int MissionId,
        string ActivationState,
        bool IsReady,
        IReadOnlyList<string> Failures)
    {
        public static MissionReadinessResponse FromDto(MissionReadinessDto dto)
        {
            return new MissionReadinessResponse(
                dto.MissionId,
                dto.ActivationState,
                dto.IsReady,
                dto.Failures?.ToList() ?? []);
        }
    }

    public sealed record MissionRuntimePlanResponse(
        string Title,
        int MaximumTime,
        IReadOnlyList<MissionRuntimeStageResponse> Stages)
    {
        public static MissionRuntimePlanResponse FromDto(MissionRuntimePlanDto dto)
        {
            return new MissionRuntimePlanResponse(
                dto.Title,
                dto.MaximumTime,
                dto.Stages.Select(MissionRuntimeStageResponse.FromDto).ToList());
        }
    }

    public sealed record MissionRuntimeStageResponse(
        string Title,
        int SequenceOrder,
        IReadOnlyList<MissionRuntimeSubstageResponse> Substages)
    {
        public static MissionRuntimeStageResponse FromDto(MissionRuntimePlanStageDto dto)
        {
            return new MissionRuntimeStageResponse(
                dto.Title,
                dto.SequenceOrder,
                dto.Substages.Select(MissionRuntimeSubstageResponse.FromDto).ToList());
        }
    }

    public sealed record MissionRuntimeSubstageResponse(
        string Title,
        int SequenceOrder,
        string PlayMode,
        int? WinnerScore,
        IReadOnlyList<MissionRuntimeTargetResponse> Targets,
        IReadOnlyList<MissionRuntimeTriviaQuestionResponse> TriviaQuestions)
    {
        public static MissionRuntimeSubstageResponse FromDto(MissionRuntimePlanSubstageDto dto)
        {
            return new MissionRuntimeSubstageResponse(
                dto.Title,
                dto.SequenceOrder,
                dto.PlayMode,
                dto.WinnerScore,
                dto.Targets.Select(MissionRuntimeTargetResponse.FromDto).ToList(),
                dto.TriviaQuestions.Select(MissionRuntimeTriviaQuestionResponse.FromDto).ToList());
        }
    }

    public sealed record MissionRuntimeTargetResponse(
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive,
        MissionRuntimeClueResponse? Clue)
    {
        public static MissionRuntimeTargetResponse FromDto(MissionRuntimePlanTargetDto dto)
        {
            return new MissionRuntimeTargetResponse(
                dto.Name,
                dto.QrCode,
                dto.SequenceOrder,
                dto.IsActive,
                dto.Clue is null ? null : MissionRuntimeClueResponse.FromDto(dto.Clue));
        }
    }

    public sealed record MissionRuntimeClueResponse(
        string Text,
        string VisibilityPolicy)
    {
        public static MissionRuntimeClueResponse FromDto(MissionRuntimePlanClueDto dto)
        {
            return new MissionRuntimeClueResponse(
                dto.Text,
                dto.VisibilityPolicy);
        }
    }

    public sealed record MissionRuntimeTriviaQuestionResponse(
        string Prompt,
        int SequenceOrder,
        IReadOnlyList<MissionRuntimeTriviaOptionResponse> Options,
        int ScoreValue,
        int TimeLimitSeconds)
    {
        public static MissionRuntimeTriviaQuestionResponse FromDto(MissionRuntimePlanTriviaQuestionDto dto)
        {
            return new MissionRuntimeTriviaQuestionResponse(
                dto.Prompt,
                dto.SequenceOrder,
                dto.Options.Select(MissionRuntimeTriviaOptionResponse.FromDto).ToList(),
                dto.ScoreValue,
                dto.TimeLimitSeconds);
        }
    }

    public sealed record MissionRuntimeTriviaOptionResponse(
        string OptionText,
        int SequenceOrder,
        bool IsCorrect)
    {
        public static MissionRuntimeTriviaOptionResponse FromDto(MissionRuntimePlanTriviaOptionDto dto)
        {
            return new MissionRuntimeTriviaOptionResponse(
                dto.OptionText,
                dto.SequenceOrder,
                dto.IsCorrect);
        }
    }
}
