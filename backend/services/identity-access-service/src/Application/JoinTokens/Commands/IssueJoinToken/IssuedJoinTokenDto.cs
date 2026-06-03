namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public sealed record IssuedJoinTokenDto(
    Guid JoinTokenId,
    string Token,
    Guid LiveSessionId,
    Guid TeamId,
    DateTimeOffset ExpiresAt);
