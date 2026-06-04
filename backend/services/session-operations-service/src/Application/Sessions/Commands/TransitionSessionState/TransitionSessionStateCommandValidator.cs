namespace umbral_backend.Application.Sessions.Commands.TransitionSessionState;

public sealed class TransitionSessionStateCommandValidator : AbstractValidator<TransitionSessionStateCommand>
{
    public TransitionSessionStateCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TargetState)
            .IsInEnum();

        RuleFor(command => command.Reason)
            .MaximumLength(500)
            .When(command => command.Reason is not null);
    }
}
