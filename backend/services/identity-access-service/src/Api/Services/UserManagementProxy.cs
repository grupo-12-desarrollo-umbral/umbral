using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Users.DTOs;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Api.Services;

public sealed class UserManagementProxy : IUserManagementEntryPoint
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserManagementHandler _handler;

    public UserManagementProxy(
        ICurrentUser currentUser,
        IUserManagementHandler handler)
    {
        _currentUser = currentUser;
        _handler = handler;
    }

    public Task<PagedResult<UserAccessCatalogItemDto>> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureAdministrator(_currentUser);

        return _handler.ListUsersAsync(page, pageSize, cancellationToken);
    }

    public Task AssignUserRoleAsync(int userId, string role, CancellationToken cancellationToken)
    {
        EnsureAdministrator(_currentUser);

        return _handler.AssignUserRoleAsync(userId, role, cancellationToken);
    }

    public Task DeactivateUserAccessAsync(int userId, CancellationToken cancellationToken)
    {
        EnsureAdministrator(_currentUser);

        return _handler.DeactivateUserAccessAsync(userId, cancellationToken);
    }

    private static void EnsureAdministrator(ICurrentUser currentUser)
    {
        if (string.IsNullOrWhiteSpace(currentUser.Id) ||
            string.IsNullOrWhiteSpace(currentUser.Email) ||
            string.IsNullOrWhiteSpace(currentUser.Role))
        {
            throw new UnauthorizedAccessException("Trusted gateway identity headers are required.");
        }

        var role = GatewayRoleParser.Parse(currentUser.Role);

        if (role != Role.Administrator)
        {
            throw new ForbiddenAccessException();
        }
    }
}
