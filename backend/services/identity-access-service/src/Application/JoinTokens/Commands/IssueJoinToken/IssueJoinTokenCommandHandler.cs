using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

// Real subject wrapped by JoinTokenIssuanceAuthorizationProxy (the registered IRequestHandler).
public sealed class IssueJoinTokenCommandHandler
{
    private readonly IJoinTokenRepository _joinTokenRepository;
    private readonly ICurrentActor _currentActor;
    private readonly IJoinTokenTokenService _joinTokenTokenService;
    private readonly JoinTokenPolicy _joinTokenPolicy;
    private readonly TimeProvider _timeProvider;

    public IssueJoinTokenCommandHandler(
        IJoinTokenRepository joinTokenRepository,
        ICurrentActor currentActor,
        IJoinTokenTokenService joinTokenTokenService,
        JoinTokenPolicy joinTokenPolicy,
        TimeProvider timeProvider)
    {
        _joinTokenRepository = joinTokenRepository;
        _currentActor = currentActor;
        _joinTokenTokenService = joinTokenTokenService;
        _joinTokenPolicy = joinTokenPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<IssuedJoinTokenDto> Handle(IssueJoinTokenCommand command, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

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
