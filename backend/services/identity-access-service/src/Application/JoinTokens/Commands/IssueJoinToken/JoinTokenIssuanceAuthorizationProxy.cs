using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class JoinTokenIssuanceAuthorizationProxy : IRequestHandler<IssueJoinTokenCommand, IssuedJoinTokenDto>
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IssueJoinTokenCommandHandler _inner;

    public JoinTokenIssuanceAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        IssueJoinTokenCommandHandler inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<IssuedJoinTokenDto> Handle(IssueJoinTokenCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        return await _inner.Handle(request, cancellationToken);
    }
}
