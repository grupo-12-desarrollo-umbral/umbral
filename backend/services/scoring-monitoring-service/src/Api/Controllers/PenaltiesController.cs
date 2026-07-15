using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Dtos.Scores;
using umbral_backend.Application.Scores.Commands.ApplyPenalty;
using umbral_backend.Application.Scores.Common.Authorization;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]
public sealed class PenaltiesController(ISender sender, IScoringSessionAccessResolver accessResolver) : ControllerBase
{
    [HttpPost("{liveSessionId:guid}/penalties")]
    public async Task<ActionResult<AppliedPenaltyDto>> ApplyPenaltyAsync(
        Guid liveSessionId,
        [FromBody] ApplyPenaltyCommand command,
        CancellationToken cancellationToken)
    {
        await accessResolver.EnsureAccessAsync(liveSessionId, cancellationToken);

        command = command with { LiveSessionId = liveSessionId };

        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }
}
