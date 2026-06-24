
namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class IssueJoinTokenCommandHandler : IRequestHandler<IssueJoinTokenCommand, IssuedJoinTokenDto>
{
    private readonly IIssueJoinTokenService _issueJoinTokenService;

    public IssueJoinTokenCommandHandler(IIssueJoinTokenService issueJoinTokenService)
    {
        _issueJoinTokenService = issueJoinTokenService;
    }

    public async Task<IssuedJoinTokenDto> Handle(IssueJoinTokenCommand request, CancellationToken cancellationToken)
    {
        return await _issueJoinTokenService.IssueAsync(request, cancellationToken);
    }
}
