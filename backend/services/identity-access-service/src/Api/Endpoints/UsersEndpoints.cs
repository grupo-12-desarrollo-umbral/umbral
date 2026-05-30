using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.DTOs;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

namespace umbral_backend.Api.Endpoints;

public sealed class UsersEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var users = groupBuilder.MapGroup("/api/users");

        users.MapPost("/authenticated", BootstrapAuthenticatedUserAsync);
        users.MapGet("/me", GetCurrentAuthenticatedUserAsync);
    }

    private static async Task<Ok<AuthenticateUserResultDto>> BootstrapAuthenticatedUserAsync(
        ISender sender,
        ICurrentUser currentUser,
        BootstrapAuthenticatedUserRequest request,
        CancellationToken cancellationToken)
    {
        EnsureTrustedIdentity(currentUser);

        var result = await sender.Send(
            new AuthenticateUserCommand(
                currentUser.Id!,
                request.DisplayName,
                currentUser.Email!,
                currentUser.Role!),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<AuthenticatedActorProfileDto>> GetCurrentAuthenticatedUserAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAuthenticatedActorProfileQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static void EnsureTrustedIdentity(ICurrentUser currentUser)
    {
        if (string.IsNullOrWhiteSpace(currentUser.Id) ||
            string.IsNullOrWhiteSpace(currentUser.Email) ||
            string.IsNullOrWhiteSpace(currentUser.Role))
        {
            throw new UnauthorizedAccessException("Trusted gateway identity headers are required.");
        }
    }

    public sealed record BootstrapAuthenticatedUserRequest(string DisplayName);
}
