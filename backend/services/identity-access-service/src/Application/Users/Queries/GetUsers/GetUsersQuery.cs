using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Users.Queries.GetUsers;

[Authorize(Roles = "Administrator,Operator")]
public sealed record GetUsersQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<UserAccessCatalogItemDto>>;
