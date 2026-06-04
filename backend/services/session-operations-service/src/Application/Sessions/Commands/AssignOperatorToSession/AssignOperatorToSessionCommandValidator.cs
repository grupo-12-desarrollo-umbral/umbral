namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public sealed class AssignOperatorToSessionCommandValidator : AbstractValidator<AssignOperatorToSessionCommand>
{
    public AssignOperatorToSessionCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.OperatorUserId)
            .GreaterThan(0);
    }
}
