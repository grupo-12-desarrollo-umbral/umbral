namespace umbral_backend.Application.Teams.Commands.AuthorizeParticipantForTeam;

public sealed class AuthorizeParticipantForTeamCommandValidator : AbstractValidator<AuthorizeParticipantForTeamCommand>
{
    public AuthorizeParticipantForTeamCommandValidator()
    {
        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .GreaterThan(0);
    }
}
