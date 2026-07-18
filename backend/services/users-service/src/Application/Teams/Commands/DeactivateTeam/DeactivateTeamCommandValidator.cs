namespace umbral_backend.Application.Teams.Commands.DeactivateTeam;

public sealed class DeactivateTeamCommandValidator : AbstractValidator<DeactivateTeamCommand>
{
    public DeactivateTeamCommandValidator()
    {
        RuleFor(command => command.TeamId)
            .NotEmpty();
    }
}
