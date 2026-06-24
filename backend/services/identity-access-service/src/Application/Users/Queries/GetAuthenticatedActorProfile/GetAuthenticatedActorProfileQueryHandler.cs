using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Users.Common;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

public sealed class GetAuthenticatedActorProfileQueryHandler : IRequestHandler<GetAuthenticatedActorProfileQuery, AuthenticatedActorProfileDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    public GetAuthenticatedActorProfileQueryHandler(IUserRepository userRepository, ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<AuthenticatedActorProfileDto> Handle(
        GetAuthenticatedActorProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        return new AuthenticatedActorProfileDto(
            user.Id,
            user.ExternalIdentityId,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            user.IsActive);
    }
}
