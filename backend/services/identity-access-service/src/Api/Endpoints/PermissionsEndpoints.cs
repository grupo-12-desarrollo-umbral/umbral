using Microsoft.AspNetCore.Http.HttpResults;
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
}
