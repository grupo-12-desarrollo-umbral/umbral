using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Application.Users.Queries.GetUsers;

[Authorize(Roles = "Administrator")]
public sealed record GetUsersQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<UserAccessCatalogItemDto>>;
