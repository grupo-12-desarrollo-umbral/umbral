using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Api.Services;

public interface IUserManagementHandler
{
    Task<PagedResult<UserAccessCatalogItemDto>> ListUsersAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task AssignUserRoleAsync(int userId, string role, CancellationToken cancellationToken);

    Task DeactivateUserAccessAsync(int userId, CancellationToken cancellationToken);
}
