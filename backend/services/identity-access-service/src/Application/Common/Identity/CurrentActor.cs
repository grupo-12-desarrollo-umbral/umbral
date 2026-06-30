using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Identity;

/// <summary>
/// Scoped resolver for the authenticated actor. Replaces the "null-check
/// <c>_currentUser.Id</c> → <c>GetByExternalIdentityIdAsync</c> → throw"
/// block that was copy-pasted across the identity-access application layer.
/// Memoizes within the request scope so a proxy and its inner service share
/// a single read instead of fetching the same row twice.
/// </summary>
public sealed class CurrentActor : ICurrentActor
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private User? _actor;

    public CurrentActor(ICurrentUser currentUser, IUserRepository userRepository)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
    }

    public async Task<User> GetActorAsync(CancellationToken cancellationToken)
    {
        if (_actor is not null)
        {
            return _actor;
        }

        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        return _actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);
    }
}
