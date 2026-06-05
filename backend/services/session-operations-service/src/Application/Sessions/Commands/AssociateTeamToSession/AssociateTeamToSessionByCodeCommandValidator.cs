namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

public sealed class AssociateTeamToSessionByCodeCommandValidator
    : AbstractValidator<AssociateTeamToSessionByCodeCommand>
{
    public AssociateTeamToSessionByCodeCommandValidator()
    {
        RuleFor(command => command.SessionCode)
            .NotEmpty();

        RuleFor(command => command.ReferenceTeamId)
            .NotEmpty();
    }
}
