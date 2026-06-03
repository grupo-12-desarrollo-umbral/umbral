using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

namespace umbral_backend.Api.Endpoints;

public sealed class SessionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var sessions = groupBuilder.MapGroup("/api/sessions");

        sessions.MapGet("/{code}/teams", GetSessionTeamsAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);
    }

    private static async Task<Ok<SessionTeamLobbyDto>> GetSessionTeamsAsync(
        string code,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSessionTeamsForParticipantQuery(code),
            cancellationToken);

        return TypedResults.Ok(result);
    }
}
