using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.Queries.GetUsers;

namespace umbral_backend.Api.Services;

public interface IUserManagementEntryPoint
{
    Task<PagedResult<UserAccessCatalogItemDto>> ListUsersAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task AssignUserRoleAsync(int userId, string role, CancellationToken cancellationToken);

    Task DeactivateUserAccessAsync(int userId, CancellationToken cancellationToken);
}
