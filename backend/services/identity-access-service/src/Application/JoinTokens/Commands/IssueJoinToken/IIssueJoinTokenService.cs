namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public interface IIssueJoinTokenService
{
    Task<IssuedJoinTokenDto> IssueAsync(IssueJoinTokenCommand command, CancellationToken cancellationToken);
}
