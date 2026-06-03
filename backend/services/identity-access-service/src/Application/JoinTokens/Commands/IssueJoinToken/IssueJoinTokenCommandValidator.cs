namespace umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class IssueJoinTokenCommandValidator : AbstractValidator<IssueJoinTokenCommand>
{
    public IssueJoinTokenCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();
    }
}
