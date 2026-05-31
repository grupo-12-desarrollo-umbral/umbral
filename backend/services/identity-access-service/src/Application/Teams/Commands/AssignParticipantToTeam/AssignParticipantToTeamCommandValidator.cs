namespace umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;

public sealed class AssignParticipantToTeamCommandValidator : AbstractValidator<AssignParticipantToTeamCommand>
{
    public AssignParticipantToTeamCommandValidator()
    {
        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .GreaterThan(0);
    }
}
