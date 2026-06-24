using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Application.Users.Queries.GetUsers;

namespace umbral_backend.Api.Services;

public sealed class UserManagementHandler : IUserManagementHandler
{
    private readonly ISender _sender;

    public UserManagementHandler(ISender sender)
    {
        _sender = sender;
    }

    public Task<PagedResult<UserAccessCatalogItemDto>> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new GetUsersQuery(page, pageSize), cancellationToken);
    }

    public Task AssignUserRoleAsync(int userId, string role, CancellationToken cancellationToken)
    {
        return _sender.Send(new AssignUserRoleCommand(userId, role), cancellationToken);
    }

    public Task DeactivateUserAccessAsync(int userId, CancellationToken cancellationToken)
    {
        return _sender.Send(new DeactivateUserCommand(userId), cancellationToken);
    }
}
