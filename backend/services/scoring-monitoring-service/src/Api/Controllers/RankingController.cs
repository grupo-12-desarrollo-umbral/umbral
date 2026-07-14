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
    [AllowAnonymous]
    [HttpGet("{liveSessionId:guid}/ranking")]
    public async Task<ActionResult<RankingSnapshotDto>> GetRankingAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var snapshot = await sender.Send(
            new GetRankingSnapshotQuery(liveSessionId),
            cancellationToken);

        return Ok(snapshot);
    }
}
