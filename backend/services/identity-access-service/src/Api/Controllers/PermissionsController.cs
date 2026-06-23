using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Application.Permissions.DTOs;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
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
    [Authorize(Policy = AuthorizationPolicies.Participant)]
    public async Task<ActionResult<ParticipantMembershipAccessDecisionDto>> ValidateParticipantMembershipAccessAsync(
        ValidateParticipantMembershipAccessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ValidateParticipantMembershipAccessQuery(request.LiveSessionId, request.TeamId, request.Token),
            cancellationToken);

        return Ok(result);
    }

    public sealed record ValidateParticipantMembershipAccessRequest(
        Guid LiveSessionId,
        Guid TeamId,
        string? Token);
}
