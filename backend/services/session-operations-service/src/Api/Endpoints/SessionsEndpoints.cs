using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Api.Endpoints;

public sealed class SessionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var sessions = groupBuilder.MapGroup("/api/sessions");
        sessions.MapPost("/{liveSessionId:guid}/participants/reconnect", ReconnectParticipantAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);
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

    public sealed record ReconnectParticipantRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);
}
