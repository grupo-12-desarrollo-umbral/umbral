using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Dtos.Users;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Queries.GetUsers;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserAccessCatalogItemDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public GetUsersQueryHandler(
        IUserRepository userRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _userRepository = userRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task<PagedResult<UserAccessCatalogItemDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.UserAccessCatalog);

        var users = await _userRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        return new PagedResult<UserAccessCatalogItemDto>
        {
            Items = users.Items.Select(MapItem).ToArray(),
            TotalCount = users.TotalCount,
            Page = users.Page,
            PageSize = users.PageSize
        };
    }

    private static UserAccessCatalogItemDto MapItem(User user)
    {
        return new UserAccessCatalogItemDto(
            user.Id,
            user.ExternalIdentityId,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            user.IsActive);
    }
}
