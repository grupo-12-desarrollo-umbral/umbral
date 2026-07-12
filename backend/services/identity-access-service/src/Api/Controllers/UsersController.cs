using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Dtos.Users;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Users.Queries.GetUsers;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.Commands.InviteUser;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Application.Users.Commands.ReactivateUser;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpPost("authenticated")]
    public async Task<ActionResult<AuthenticateUserResultDto>> BootstrapAuthenticatedUserAsync(
        BootstrapAuthenticatedUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AuthenticateUserCommand(request.DisplayName), cancellationToken);

        return Ok(result);
    }

    [HttpPost("invitations")]
    public async Task<ActionResult<InviteUserResultDto>> InviteUserAsync(
        InviteUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InviteUserCommand(request.Email, request.Role), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
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
        var result = await sender.Send(
            new GetUsersQuery(request.Page, request.PageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:int}/access")]
    public async Task<IActionResult> DeactivateUserAccessAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateUserCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/access")]
    public async Task<IActionResult> ReactivateUserAccessAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ReactivateUserCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> AssignUserRoleAsync(
        int id,
        AssignUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AssignUserRoleCommand(id, request.Role), cancellationToken);
        return NoContent();
    }

    public sealed record BootstrapAuthenticatedUserRequest(string DisplayName);

    public sealed record InviteUserRequest(string Email, string Role);

    public sealed record GetUsersRequest(int Page = 1, int PageSize = 20);

    public sealed record AssignUserRoleRequest(string Role);
}
