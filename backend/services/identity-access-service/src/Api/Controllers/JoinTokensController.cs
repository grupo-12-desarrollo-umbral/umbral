using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/join-tokens")]
public sealed class JoinTokensController(ISender sender, TimeProvider timeProvider) : ControllerBase
{
    private static readonly TimeSpan DefaultJoinTokenLifetime = TimeSpan.FromMinutes(15);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOperator)]
    public async Task<ActionResult<IssuedJoinTokenDto>> IssueJoinTokenAsync(
        IssueJoinTokenRequest request,
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

        return Created($"/api/join-tokens/{result.JoinTokenId}", result);
    }

    public sealed record IssueJoinTokenRequest(
        Guid LiveSessionId,
        Guid TeamId,
        int? ExpiresInSeconds);
}
