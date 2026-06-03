using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

namespace umbral_backend.Api.Endpoints;

public sealed class JoinTokensEndpoints : IEndpointGroup
{
    private static readonly TimeSpan DefaultJoinTokenLifetime = TimeSpan.FromMinutes(15);

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost("/api/join-tokens", IssueJoinTokenAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrOperator);
    }

    private static async Task<Created<IssuedJoinTokenDto>> IssueJoinTokenAsync(
        IssueJoinTokenRequest request,
        ISender sender,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var expiresInSeconds = request.ExpiresInSeconds.GetValueOrDefault();
        var expiresAt = timeProvider.GetUtcNow().Add(
            expiresInSeconds > 0
                ? TimeSpan.FromSeconds(expiresInSeconds)
                : DefaultJoinTokenLifetime);

        var result = await sender.Send(
            new IssueJoinTokenCommand(request.LiveSessionId, request.TeamId, expiresAt),
            cancellationToken);

        return TypedResults.Created($"/api/join-tokens/{result.JoinTokenId}", result);
    }

    public sealed record IssueJoinTokenRequest(
        Guid LiveSessionId,
        Guid TeamId,
        int? ExpiresInSeconds);
}
