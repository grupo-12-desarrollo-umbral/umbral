namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class JoinTeamAsParticipantCommandValidator : AbstractValidator<JoinTeamAsParticipantCommand>
{
    public JoinTeamAsParticipantCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();
    }
}
