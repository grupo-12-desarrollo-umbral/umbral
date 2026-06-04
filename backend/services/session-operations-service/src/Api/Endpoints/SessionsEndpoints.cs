using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Api.Endpoints;

public sealed class SessionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var sessions = groupBuilder.MapGroup("/api/sessions");
        sessions.MapPost("/{liveSessionId:guid}/teams", AssociateTeamAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);
        sessions.MapGet("/{liveSessionId:guid}/teams", GetAssociatedTeamsAsync)
            .RequireAuthorization(AuthorizationPolicies.Operator);
        sessions.MapPost("/{liveSessionId:guid}/participants/reconnect", ReconnectParticipantAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);
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
                request.TeamCapacity,
                request.Token),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    public sealed record AssociateTeamRequest(Guid ReferenceTeamId);

    public sealed record ReconnectParticipantRequest(
        Guid TeamId,
        string DisplayName,
        int TeamCapacity,
        string? Token);
}
