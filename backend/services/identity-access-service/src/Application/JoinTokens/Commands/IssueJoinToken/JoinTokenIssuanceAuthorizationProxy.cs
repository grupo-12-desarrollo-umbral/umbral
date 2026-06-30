using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class JoinTokenIssuanceAuthorizationProxy : IIssueJoinTokenService
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IIssueJoinTokenService _inner;

    public JoinTokenIssuanceAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        IIssueJoinTokenService inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<IssuedJoinTokenDto> IssueAsync(IssueJoinTokenCommand command, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        return await _inner.IssueAsync(command, cancellationToken);
    }
}
