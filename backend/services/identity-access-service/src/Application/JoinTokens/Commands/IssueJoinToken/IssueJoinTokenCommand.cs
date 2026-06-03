using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

[Authorize(Roles = "Administrator,Operator")]
public sealed record IssueJoinTokenCommand(
    Guid LiveSessionId,
    Guid TeamId,
    DateTimeOffset ExpiresAt) : IRequest<IssuedJoinTokenDto>;
