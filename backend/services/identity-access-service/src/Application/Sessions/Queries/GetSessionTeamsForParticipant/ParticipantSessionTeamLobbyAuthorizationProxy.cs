using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed class ParticipantSessionTeamLobbyAuthorizationProxy : IGetSessionTeamsForParticipantService
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;
    private readonly IGetSessionTeamsForParticipantExecutor _inner;

    public ParticipantSessionTeamLobbyAuthorizationProxy(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy,
        IGetSessionTeamsForParticipantExecutor inner)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<SessionTeamLobbyDto> GetAsync(
        GetSessionTeamsForParticipantQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.ParticipantExperience);

        return await _inner.GetAsync(query, cancellationToken);
    }
}
