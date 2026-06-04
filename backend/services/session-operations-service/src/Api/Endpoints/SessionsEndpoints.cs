using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Commands.CreateTriviaSession;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.Commands.TransitionSessionState;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;
using umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Endpoints;

public sealed class SessionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var sessions = groupBuilder.MapGroup("/api/sessions");

        sessions.MapPost("/", CreateTriviaSessionAsync)
            .RequireAuthorization(AuthorizationPolicies.Administrator);

        sessions.MapGet("/", ListAssignableSessionsAsync)
            .RequireAuthorization(AuthorizationPolicies.AdministratorOrOperator);

        sessions.MapPatch("/{liveSessionId:guid}/operator-assignment", AssignOperatorAsync)
            .RequireAuthorization(AuthorizationPolicies.Administrator);

        sessions.MapPatch("/{liveSessionId:guid}/state", TransitionSessionStateAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);

        sessions.MapPost("/{liveSessionId:guid}/teams", AssociateTeamAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);

        sessions.MapGet("/{liveSessionId:guid}/teams", GetAssociatedTeamsAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);

        sessions.MapPost("/{liveSessionId:guid}/participants/reconnect", ReconnectParticipantAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);

        sessions.MapGet("/{liveSessionId:guid}/participants/timer", GetParticipantTimerSnapshotAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);

        sessions.MapGet("/{liveSessionId:guid}/timer", GetOperatorTimerSnapshotAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);
    }

    private static async Task<Created<CreateTriviaSessionResultDto>> CreateTriviaSessionAsync(
        CreateTriviaSessionRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateTriviaSessionCommand(
                request.SourceTriviaQuizId,
                request.Title,
                request.MaximumTimeMinutes,
                request.ScheduledAt),
            cancellationToken);

        return TypedResults.Created($"/api/sessions/{result.LiveSessionId:D}", result);
    }

    private static async Task<Ok<IReadOnlyList<SessionOperatorSummaryDto>>> ListAssignableSessionsAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAssignableSessionsQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<AssociateTeamToSessionResultDto>> AssociateTeamAsync(
        Guid liveSessionId,
        AssociateTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssociateTeamToSessionCommand(liveSessionId, request.ReferenceTeamId),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<SessionAssociatedTeamsDto>> GetAssociatedTeamsAsync(
        Guid liveSessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAssociatedTeamsForSessionQuery(liveSessionId), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ReconnectParticipantResultDto>> ReconnectParticipantAsync(
        Guid liveSessionId,
        ReconnectParticipantRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReconnectAuthenticatedParticipantCommand(
                liveSessionId,
                request.TeamId,
                request.DisplayName,
                request.Token),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<SessionTimerSnapshotDto>> GetParticipantTimerSnapshotAsync(
        Guid liveSessionId,
        Guid teamId,
        string? token,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetParticipantSessionTimerSnapshotQuery(liveSessionId, teamId, token),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<SessionTimerSnapshotDto>> GetOperatorTimerSnapshotAsync(
        Guid liveSessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetOperatorSessionTimerSnapshotQuery(liveSessionId),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<AssignOperatorToSessionResultDto>> AssignOperatorAsync(
        Guid liveSessionId,
        AssignOperatorRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignOperatorToSessionCommand(liveSessionId, request.OperatorUserId),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<TransitionSessionStateResultDto>, ProblemHttpResult>> TransitionSessionStateAsync(
        Guid liveSessionId,
        TransitionSessionStateRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SessionState>(request.TargetState, ignoreCase: true, out var targetState) ||
            !Enum.IsDefined(targetState))
        {
            return TypedResults.Problem(
                detail: $"'{request.TargetState}' is not a valid session state.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed.");
        }

        var result = await sender.Send(
            new TransitionSessionStateCommand(liveSessionId, targetState, request.Reason),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    public sealed record CreateTriviaSessionRequest(
        int SourceTriviaQuizId,
        string Title,
        int MaximumTimeMinutes,
        DateTimeOffset ScheduledAt);

    public sealed record AssociateTeamRequest(Guid ReferenceTeamId);

    public sealed record ReconnectParticipantRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);

    public sealed record AssignOperatorRequest(int OperatorUserId);

    public sealed record TransitionSessionStateRequest(string TargetState, string? Reason);
}
