using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Commands.CreateTriviaSession;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

namespace umbral_backend.Api.Endpoints;

public sealed class SessionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var sessions = groupBuilder.MapGroup("/api/sessions");

        sessions.MapPost("/", CreateTriviaSessionAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);

        sessions.MapGet("/", ListAssignableSessionsAsync)
            .RequireAuthorization(AuthorizationPolicies.Administrator);

        sessions.MapPatch("/{liveSessionId:guid}/operator-assignment", AssignOperatorAsync)
            .RequireAuthorization(AuthorizationPolicies.Administrator);

        sessions.MapPost("/{liveSessionId:guid}/participants/reconnect", ReconnectParticipantAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);
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

    public sealed record CreateTriviaSessionRequest(
        int SourceTriviaQuizId,
        string Title,
        int MaximumTimeMinutes,
        DateTimeOffset ScheduledAt);

    public sealed record ReconnectParticipantRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);

    public sealed record AssignOperatorRequest(int OperatorUserId);
}
