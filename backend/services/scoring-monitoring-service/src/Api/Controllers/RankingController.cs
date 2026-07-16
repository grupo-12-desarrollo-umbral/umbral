using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Dtos.Rankings;
using umbral_backend.Application.Rankings.Queries.GetOperatorRankingSnapshot;
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

    // Operator read of the same session-wide standings. Separate from the participant route above because
    // the audiences authorize differently: a participant proves team membership, an operator proves the
    // session is assigned to them. Same snapshot either way — the ranking is not audience-shaped.
    [Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]
    [HttpGet("{liveSessionId:guid}/ranking/operator")]
    public async Task<ActionResult<RankingSnapshotDto>> GetOperatorRankingAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var snapshot = await sender.Send(
            new GetOperatorRankingSnapshotQuery(liveSessionId),
            cancellationToken);

        return Ok(snapshot);
    }
}
