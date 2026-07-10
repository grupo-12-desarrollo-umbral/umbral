using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;

namespace umbral_backend.Web.Controllers;

[ApiController]
[Route("api/missions")]
public sealed class MissionsController(ISender sender) : ControllerBase
{
    [HttpGet("difficulties")]
    public async Task<ActionResult<IReadOnlyList<DifficultyResponse>>> GetDifficultyCatalog(
        CancellationToken cancellationToken)
    {
        var difficulties = await sender.Send(new GetDifficultyCatalogQuery(), cancellationToken);
        IReadOnlyList<DifficultyResponse> response = difficulties
            .Select(DifficultyResponse.FromDto)
            .ToList();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<MissionResponse>> CreateMission(
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

        return Created($"/api/missions/{mission.Id}", MissionResponse.FromDto(mission));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MissionSummaryResponse>>> GetMissionCatalog(
        CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new GetMissionCatalogQuery(), cancellationToken);
        IReadOnlyList<MissionSummaryResponse> response = missions
            .Select(MissionSummaryResponse.FromDto)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MissionResponse>> GetMissionDetail(
        int id,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetMissionDetailQuery(id), cancellationToken);
        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpGet("{id:int}/runtime-plan")]
    public async Task<ActionResult<MissionRuntimePlanResponse>> GetMissionRuntimePlan(
        int id,
        CancellationToken cancellationToken)
    {
        var missionRuntimePlan = await sender.Send(new GetMissionRuntimePlanQuery(id), cancellationToken);
        return Ok(MissionRuntimePlanResponse.FromDto(missionRuntimePlan));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MissionResponse>> UpdateMission(
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

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeactivateMission(
        int id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateMissionCommand(id), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<MissionResponse>> ActivateMission(
        int id,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new ActivateMissionCommand(id), cancellationToken);
        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpGet("{id:int}/readiness")]
    public async Task<ActionResult<MissionReadinessResponse>> GetMissionReadiness(
        int id,
        CancellationToken cancellationToken)
    {
        var readiness = await sender.Send(new GetMissionReadinessQuery(id), cancellationToken);
        return Ok(MissionReadinessResponse.FromDto(readiness));
    }

    [HttpPost("{missionId:int}/nodes")]
    public async Task<ActionResult<MissionResponse>> AddMissionNode(
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

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPut("{missionId:int}/nodes/{nodeId:int}")]
    public async Task<ActionResult<MissionResponse>> UpdateMissionNode(
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

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpDelete("{missionId:int}/nodes/{nodeId:int}")]
    public async Task<ActionResult<MissionResponse>> RemoveMissionNode(
        int missionId,
        int nodeId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new RemoveMissionNodeCommand(missionId, nodeId), cancellationToken);
        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPut("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/play-mode")]
    public async Task<ActionResult<MissionResponse>> AssignSubstagePlayMode(
        int missionId,
        int stageId,
        int substageId,
        AssignSubstagePlayModeRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new AssignSubstagePlayModeCommand(missionId, stageId, substageId, request.PlayMode),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPost("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets")]
    public async Task<ActionResult<MissionResponse>> AddTarget(
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
                request.IsActive),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPut("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}")]
    public async Task<ActionResult<MissionResponse>> UpdateTarget(
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
                request.IsActive),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpDelete("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}")]
    public async Task<ActionResult<MissionResponse>> RemoveTarget(
        int missionId,
        int stageId,
        int substageId,
        int targetId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new RemoveTargetCommand(missionId, stageId, substageId, targetId),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPost("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}/clue-association")]
    public async Task<ActionResult<MissionResponse>> AssociateClueWithTarget(
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

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpDelete("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/targets/{targetId:int}/clue-association")]
    public async Task<ActionResult<MissionResponse>> UnassociateClueFromTarget(
        int missionId,
        int stageId,
        int substageId,
        int targetId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UnassociateClueFromTargetCommand(missionId, stageId, substageId, targetId),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPost("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/trivia-quiz-selection")]
    public async Task<ActionResult<MissionResponse>> SetTriviaQuizSelection(
        int missionId,
        int stageId,
        int substageId,
        TriviaQuizSelectionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new SelectTriviaQuizCommand(missionId, stageId, substageId, request.TriviaQuizId),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
    }

    [HttpPut("{missionId:int}/stages/{stageId:int}/substages/{substageId:int}/trivia-quiz-selection")]
    public async Task<ActionResult<MissionResponse>> UpdateTriviaQuizSelection(
        int missionId,
        int stageId,
        int substageId,
        TriviaQuizSelectionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new SelectTriviaQuizCommand(missionId, stageId, substageId, request.TriviaQuizId),
            cancellationToken);

        return Ok(MissionResponse.FromDto(mission));
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
        bool IsActive = true);

    public sealed record UpdateTargetRequest(
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive);

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
        int? ClueId,
        int Score)
    {
        public static MissionTargetResponse FromDto(MissionTargetDto dto)
        {
            return new MissionTargetResponse(
                dto.Id,
                dto.Name,
                dto.QrCode,
                dto.SequenceOrder,
                dto.IsActive,
                dto.ClueId,
                dto.Score);
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
        IReadOnlyList<MissionRuntimeTargetResponse> Targets,
        IReadOnlyList<MissionRuntimeTriviaQuestionResponse> TriviaQuestions)
    {
        public static MissionRuntimeSubstageResponse FromDto(MissionRuntimePlanSubstageDto dto)
        {
            return new MissionRuntimeSubstageResponse(
                dto.Title,
                dto.SequenceOrder,
                dto.PlayMode,
                dto.Targets.Select(MissionRuntimeTargetResponse.FromDto).ToList(),
                dto.TriviaQuestions.Select(MissionRuntimeTriviaQuestionResponse.FromDto).ToList());
        }
    }

    public sealed record MissionRuntimeTargetResponse(
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive,
        int Score,
        MissionRuntimeClueResponse? Clue)
    {
        public static MissionRuntimeTargetResponse FromDto(MissionRuntimePlanTargetDto dto)
        {
            return new MissionRuntimeTargetResponse(
                dto.Name,
                dto.QrCode,
                dto.SequenceOrder,
                dto.IsActive,
                dto.Score,
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
