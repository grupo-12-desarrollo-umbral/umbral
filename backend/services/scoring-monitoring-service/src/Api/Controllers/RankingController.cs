using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Dtos.Rankings;
using umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class RankingController(ISender sender) : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.ParticipantOrOperator)]
    [HttpGet("{liveSessionId:guid}/ranking")]
    public async Task<ActionResult<RankingSnapshotDto>> GetRankingAsync(
        Guid liveSessionId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var snapshot = await sender.Send(
            new GetRankingSnapshotQuery(liveSessionId, teamId),
            cancellationToken);

        return Ok(snapshot);
    }
}
