using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.DTOs;
using umbral_backend.Application.Users.Queries.GetUsers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Handlers;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserAccessCatalogItemDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;

    public GetUsersQueryHandler(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<PagedResult<UserAccessCatalogItemDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        EnsureActorCanListUsers(actor);

        var users = await _userRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        return new PagedResult<UserAccessCatalogItemDto>
        {
            Items = users.Items.Select(MapItem).ToArray(),
            TotalCount = users.TotalCount,
            Page = users.Page,
            PageSize = users.PageSize
        };
    }

    private void EnsureActorCanListUsers(User actor)
    {
        var decision = _accessPolicy.Evaluate(actor, ProtectedCapability.UserAccessCatalog);

        if (!actor.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(actor.Id);
        }

        if (!decision.IsAllowed)
        {
            throw new UserRoleNotAuthorizedException(actor.Role, ProtectedCapability.UserAccessCatalog);
        }
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
