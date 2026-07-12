using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.Commands.SelectTeam;
using umbral_backend.Application.Sessions.Commands.SubmitTriviaAnswer;
using umbral_backend.Application.Sessions.Commands.TransitionSessionState;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionPanel;
using umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnsweredMonitor;
using umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;
using umbral_backend.Application.Sessions.Queries.GetParticipantTeamBoard;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamLobby;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<ActionResult<CreateSessionResultDto>> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSessionCommand(
                request.MissionId,
                request.Title,
                request.MaximumTimeMinutes,
                request.ScheduledAt),
            cancellationToken);

        return Created($"/api/sessions/{result.LiveSessionId:D}", result);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]
    public async Task<ActionResult<IReadOnlyList<SessionOperatorSummaryDto>>> ListAssignableSessionsAsync(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAssignableSessionsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{liveSessionId:guid}/operator-assignment")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<ActionResult<AssignOperatorToSessionResultDto>> AssignOperatorAsync(
        Guid liveSessionId,
        AssignOperatorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignOperatorToSessionCommand(liveSessionId, request.OperatorUserId),
            cancellationToken);

        return Ok(result);
    }

    [HttpPatch("{liveSessionId:guid}/state")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<TransitionSessionStateResultDto>> TransitionSessionStateAsync(
        Guid liveSessionId,
        TransitionSessionStateRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SessionState>(request.TargetState, ignoreCase: true, out var targetState) ||
            !Enum.IsDefined(targetState))
        {
            return Problem(
                detail: $"'{request.TargetState}' is not a valid session state.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed.");
        }

        var result = await sender.Send(
            new TransitionSessionStateCommand(liveSessionId, targetState, request.Reason),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/teams")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<AssociateTeamToSessionResultDto>> AssociateTeamAsync(
        Guid liveSessionId,
        AssociateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssociateTeamToSessionCommand(liveSessionId, request.ReferenceTeamId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{liveSessionId:guid}/teams")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<SessionAssociatedTeamsDto>> GetAssociatedTeamsAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAssociatedTeamsForSessionQuery(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("by-code/{sessionCode}/teams")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<AssociateTeamToSessionResultDto>> AssociateTeamByCodeAsync(
        string sessionCode,
        AssociateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssociateTeamToSessionByCodeCommand(sessionCode, request.ReferenceTeamId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("by-code/{sessionCode}/teams")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<SessionAssociatedTeamsDto>> GetAssociatedTeamsByCodeAsync(
        string sessionCode,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAssociatedTeamsForSessionByCodeQuery(sessionCode),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/participants/reconnect")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<ReconnectParticipantResultDto>> ReconnectParticipantAsync(
        Guid liveSessionId,
        ReconnectParticipantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReconnectAuthenticatedParticipantCommand(
                liveSessionId,
                request.TeamId,
                request.DisplayName,
                request.Token),
            cancellationToken);

        return Ok(result);
    }

    // HU-34: participant submits the team's answer to the active trivia question. First-write-wins —
    // only the first in-time answer succeeds (200 + acceptance metadata only); late/duplicate/no-active
    // -question attempts surface as RFC 7807 ProblemDetails via the global handler. Correctness/points
    // never leak here. The snapshotted question is keyed by (substage snapshot id + question order)
    // and the option by its order, because the frozen snapshot carries no per-item Guids.
    [HttpPost("{liveSessionId:guid}/participants/answers")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<SubmitTriviaAnswerResultDto>> SubmitTriviaAnswerAsync(
        Guid liveSessionId,
        SubmitTriviaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SubmitTriviaAnswerCommand(
                liveSessionId,
                request.TeamId,
                request.TriviaSubstageSnapshotId,
                request.QuestionSequenceOrder,
                request.SelectedOptionSequenceOrder,
                request.Token),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{liveSessionId:guid}/participants/timer")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<SessionTimerSnapshotDto>> GetParticipantTimerSnapshotAsync(
        Guid liveSessionId,
        Guid teamId,
        string? token,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetParticipantSessionTimerSnapshotQuery(liveSessionId, teamId, token),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{liveSessionId:guid}/participants/team-board")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<ParticipantTeamBoardDto>> GetParticipantTeamBoardAsync(
        Guid liveSessionId,
        Guid teamId,
        string? token,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetParticipantTeamBoardQuery(liveSessionId, teamId, token),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("by-code/{sessionCode}/teams/lobby")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<SessionTeamLobbyDto>> GetSessionTeamLobbyByCodeAsync(
        string sessionCode,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSessionTeamLobbyByCodeQuery(sessionCode), cancellationToken);
        return Ok(result);
    }

    [HttpPost("by-code/{sessionCode}/teams/{runtimeTeamId:guid}/join")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<SelectTeamResultDto>> SelectTeamAsync(
        string sessionCode,
        Guid runtimeTeamId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SelectTeamCommand(sessionCode, runtimeTeamId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{liveSessionId:guid}/timer")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<SessionTimerSnapshotDto>> GetOperatorTimerSnapshotAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetOperatorSessionTimerSnapshotQuery(liveSessionId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{liveSessionId:guid}/operator-panel")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<OperatorSessionPanelDto>> GetOperatorSessionPanelAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetOperatorSessionPanelQuery(liveSessionId),
            cancellationToken);

        return Ok(result);
    }

    // HU-36A: pre-close restricted answered/not-answered monitor for the active synchronized trivia
    // question. Operator-scoped by the coarse policy; per-session ownership stays delegated to the
    // resolver Proxy in the handler, so ForbiddenAccessException maps to 403 for a non-owning operator.
    // The payload carries only per-team answered/answeredAt + the active-question identity — never the
    // chosen option/correctness/points. Live updates ride the reused HU-34 operator-only TeamAnswered
    // event; this endpoint only serves the initial snapshot on connect/refresh.
    [HttpGet("{liveSessionId:guid}/answered-monitor")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    public async Task<ActionResult<TriviaAnsweredMonitorDto>> GetTriviaAnsweredMonitorAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetOperatorTriviaAnsweredMonitorQuery(liveSessionId),
            cancellationToken);

        return Ok(result);
    }

    public sealed record CreateSessionRequest(
        int MissionId,
        string Title,
        int MaximumTimeMinutes,
        DateTimeOffset ScheduledAt);

    public sealed record AssociateTeamRequest(Guid ReferenceTeamId);

    public sealed record ReconnectParticipantRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);

    public sealed record SubmitTriviaAnswerRequest(
        Guid TeamId,
        Guid TriviaSubstageSnapshotId,
        int QuestionSequenceOrder,
        int SelectedOptionSequenceOrder,
        string? Token);

    public sealed record AssignOperatorRequest(int OperatorUserId);

    public sealed record TransitionSessionStateRequest(string TargetState, string? Reason);
}
