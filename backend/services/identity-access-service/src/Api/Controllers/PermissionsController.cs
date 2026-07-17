using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Dtos.Permissions;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Application.Permissions.Queries.GetParticipantEligibleTeams;
using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Controllers;

// Authenticated-user check only, deliberately not a role policy: these routes answer "is this actor
// eligible" with a reason-coded 200 (e.g. `UserNotParticipant`), which callers depend on to
// distinguish a denied user from an empty result. A role gate would collapse that into a 403.
[ApiController]
[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController(ISender sender) : ControllerBase
{
    [HttpGet("authenticated-platform-access")]
    public async Task<ActionResult<ProtectedAccessDecisionDto>> CheckAuthenticatedPlatformAccessAsync(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CheckProtectedCapabilityAccessQuery(ProtectedCapability.AuthenticatedPlatformAccess),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("participant-membership-access")]
    public async Task<ActionResult<ParticipantMembershipAccessDecisionDto>> ValidateParticipantMembershipAccessAsync(
        ValidateParticipantMembershipAccessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ValidateParticipantMembershipAccessQuery(request.LiveSessionId, request.TeamId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("participant-eligible-teams")]
    public async Task<ActionResult<ParticipantEligibleTeamsDto>> GetParticipantEligibleTeamsAsync(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetParticipantEligibleTeamsQuery(), cancellationToken);

        return Ok(result);
    }

    // Join-token validation moved to session-operations (issue #87); Users answers team-membership
    // eligibility only. A stray `token` field from an older client is ignored by the JSON binder.
    public sealed record ValidateParticipantMembershipAccessRequest(
        Guid LiveSessionId,
        Guid TeamId);
}
