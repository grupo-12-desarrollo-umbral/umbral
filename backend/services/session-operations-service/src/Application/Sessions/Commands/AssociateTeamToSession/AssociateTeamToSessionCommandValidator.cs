namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

public sealed class AssociateTeamToSessionCommandValidator : AbstractValidator<AssociateTeamToSessionCommand>
{
    public AssociateTeamToSessionCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.ReferenceTeamId)
            .NotEmpty();
    }
}
