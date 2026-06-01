using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Common.Models;
using umbral_backend.Api.Services;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Application.Users.DTOs;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Application.Users.Queries.GetUsers;

namespace umbral_backend.Api.Endpoints;

public sealed class UsersEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var users = groupBuilder.MapGroup("/api/users");

        users.MapPost("/authenticated", BootstrapAuthenticatedUserAsync);
        users.MapGet("/me", GetCurrentAuthenticatedUserAsync);
        users.MapGet(string.Empty, GetUsersAsync);
        users.MapPatch("/{id:int}/role", AssignUserRoleAsync);
        users.MapDelete("/{id:int}/access", DeactivateUserAccessAsync);
    }

    private static async Task<Ok<AuthenticateUserResultDto>> BootstrapAuthenticatedUserAsync(
        IAuthenticatedUserLoginEntryPoint loginEntryPoint,
        BootstrapAuthenticatedUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginEntryPoint.AuthenticateAsync(request.DisplayName, cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<AuthenticatedActorProfileDto>> GetCurrentAuthenticatedUserAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAuthenticatedActorProfileQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<PagedResult<UserAccessCatalogItemDto>>> GetUsersAsync(
        ISender sender,
        [AsParameters] GetUsersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetUsersQuery(request.Page, request.PageSize),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> DeactivateUserAccessAsync(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateUserCommand(id), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> AssignUserRoleAsync(
        int id,
        AssignUserRoleRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AssignUserRoleCommand(id, request.Role), cancellationToken);
        return TypedResults.NoContent();
    }

    public sealed record BootstrapAuthenticatedUserRequest(string DisplayName);

    public sealed record GetUsersRequest(int Page = 1, int PageSize = 20);

    public sealed record AssignUserRoleRequest(string Role);
}
