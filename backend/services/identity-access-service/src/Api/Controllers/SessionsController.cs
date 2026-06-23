using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSessionReference;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(ISender sender) : ControllerBase
{
    [HttpGet("{code}/teams")]
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<SessionTeamLobbyDto>> GetSessionTeamsAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSessionTeamsForParticipantQuery(code),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{code}/teams")]
    public async Task<IActionResult> AssociateTeamAsync(
        string code,
        AssociateTeamRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new AssociateTeamToSessionReferenceCommand(request.LiveSessionId, code, request.TeamId),
            cancellationToken);

        return Ok();
    }

    public sealed record AssociateTeamRequest(Guid LiveSessionId, Guid TeamId);
}
