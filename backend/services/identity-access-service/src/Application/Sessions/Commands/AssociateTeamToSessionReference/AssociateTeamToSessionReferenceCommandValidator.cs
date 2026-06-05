namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSessionReference;

public sealed class AssociateTeamToSessionReferenceCommandValidator
    : AbstractValidator<AssociateTeamToSessionReferenceCommand>
{
    public AssociateTeamToSessionReferenceCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.SessionCode)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();
    }
}
