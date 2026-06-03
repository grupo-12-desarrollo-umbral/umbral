using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Api.Services;
using umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Application.Permissions.DTOs;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Endpoints;

public sealed class PermissionsEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var permissions = groupBuilder.MapGroup("/api/permissions");
        permissions.MapGet("/authenticated-platform-access", CheckAuthenticatedPlatformAccessAsync);
        permissions.MapPost("/participant-membership-access", ValidateParticipantMembershipAccessAsync)
            .RequireAuthorization(AuthorizationPolicies.Participant);
    }

    private static async Task<Ok<ProtectedAccessDecisionDto>> CheckAuthenticatedPlatformAccessAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CheckProtectedCapabilityAccessQuery(ProtectedCapability.AuthenticatedPlatformAccess),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ParticipantMembershipAccessDecisionDto>> ValidateParticipantMembershipAccessAsync(
        ValidateParticipantMembershipAccessRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ValidateParticipantMembershipAccessQuery(request.LiveSessionId, request.TeamId, request.Token),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    public sealed record ValidateParticipantMembershipAccessRequest(
        Guid LiveSessionId,
        Guid TeamId,
        string? Token);
}
