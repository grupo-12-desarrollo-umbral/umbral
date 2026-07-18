namespace umbral_backend.Application.Teams.Commands.UpdateTeam;

public sealed class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.DisplayName)
            .NotEmpty();

        RuleFor(command => command.TeamCode)
            .NotEmpty();
    }
}
