namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public interface IIssueJoinTokenExecutor
{
    Task<IssuedJoinTokenDto> IssueAsync(IssueJoinTokenCommand command, CancellationToken cancellationToken);
}
