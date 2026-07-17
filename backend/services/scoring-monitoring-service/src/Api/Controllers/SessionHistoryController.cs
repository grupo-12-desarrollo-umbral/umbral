using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Dtos.SessionEvents;
using umbral_backend.Application.SessionEvents.Queries.GetSessionHistory;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class SessionHistoryController(ISender sender) : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]
    [HttpGet("{liveSessionId:guid}/history")]
    public async Task<ActionResult<SessionHistoryDto>> GetHistoryAsync(
        Guid liveSessionId,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        var history = await sender.Send(
            new GetSessionHistoryQuery(liveSessionId, teamId),
            cancellationToken);

        return Ok(history);
    }
}
