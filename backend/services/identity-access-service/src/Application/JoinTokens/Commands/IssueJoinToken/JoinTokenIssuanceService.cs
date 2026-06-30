using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class JoinTokenIssuanceService : IIssueJoinTokenService
{
    private readonly IJoinTokenRepository _joinTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IJoinTokenTokenService _joinTokenTokenService;
    private readonly JoinTokenPolicy _joinTokenPolicy;
    private readonly TimeProvider _timeProvider;

    public JoinTokenIssuanceService(
        IJoinTokenRepository joinTokenRepository,
        IUserRepository userRepository,
        ICurrentUser currentUser,
        IJoinTokenTokenService joinTokenTokenService,
        JoinTokenPolicy joinTokenPolicy,
        TimeProvider timeProvider)
    {
        _joinTokenRepository = joinTokenRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _joinTokenTokenService = joinTokenTokenService;
        _joinTokenPolicy = joinTokenPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<IssuedJoinTokenDto> IssueAsync(IssueJoinTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(Domain.Entities.User), _currentUser.Id);

        var issuedAt = _timeProvider.GetUtcNow();
        var token = _joinTokenTokenService.GenerateToken();
        var tokenHash = _joinTokenTokenService.HashToken(token);

        var joinToken = Domain.Entities.JoinToken.Issue(
            command.LiveSessionId,
            command.TeamId,
            tokenHash,
            issuedAt,
            command.ExpiresAt,
            actor.Id,
            _joinTokenPolicy);

        await _joinTokenRepository.AddAsync(joinToken, cancellationToken);

        return new IssuedJoinTokenDto(
            joinToken.JoinTokenId,
            token,
            joinToken.LiveSessionId,
            joinToken.TeamId,
            joinToken.ExpiresAt);
    }
}
