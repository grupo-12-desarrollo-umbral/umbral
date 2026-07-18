namespace umbral_backend.Application.Teams.Commands.RegisterTeam;

public sealed class RegisterTeamCommandValidator : AbstractValidator<RegisterTeamCommand>
{
    public RegisterTeamCommandValidator()
    {
        RuleFor(command => command.DisplayName)
            .NotEmpty();

        RuleFor(command => command.TeamCode)
            .NotEmpty();
    }
}
