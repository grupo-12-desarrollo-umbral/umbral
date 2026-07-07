using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/permissions")]
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

    // Join-token validation moved to session-operations (issue #87); Users answers team-membership
    // eligibility only. A stray `token` field from an older client is ignored by the JSON binder.
    public sealed record ValidateParticipantMembershipAccessRequest(
        Guid LiveSessionId,
        Guid TeamId);
}
