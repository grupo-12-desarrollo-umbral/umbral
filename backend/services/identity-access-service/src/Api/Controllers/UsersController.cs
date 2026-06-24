using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Users.Queries.GetUsers;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.Common;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(ISender sender, IUserManagementEntryPoint userManagementEntryPoint) : ControllerBase
{
    [HttpPost("authenticated")]
    public async Task<ActionResult<AuthenticateUserResultDto>> BootstrapAuthenticatedUserAsync(
        BootstrapAuthenticatedUserRequest request,
        [FromServices] IAuthenticatedUserLoginEntryPoint loginEntryPoint,
        CancellationToken cancellationToken)
    {
        var result = await loginEntryPoint.AuthenticateAsync(request.DisplayName, cancellationToken);

        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedActorProfileDto>> GetCurrentAuthenticatedUserAsync(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAuthenticatedActorProfileQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserAccessCatalogItemDto>>> GetUsersAsync(
        [FromQuery] GetUsersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userManagementEntryPoint.ListUsersAsync(
            request.Page,
            request.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:int}/access")]
    public async Task<IActionResult> DeactivateUserAccessAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await userManagementEntryPoint.DeactivateUserAccessAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> AssignUserRoleAsync(
        int id,
        AssignUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        await userManagementEntryPoint.AssignUserRoleAsync(id, request.Role, cancellationToken);
        return NoContent();
    }

    public sealed record BootstrapAuthenticatedUserRequest(string DisplayName);

    public sealed record GetUsersRequest(int Page = 1, int PageSize = 20);

    public sealed record AssignUserRoleRequest(string Role);
}
