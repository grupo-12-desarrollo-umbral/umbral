namespace umbral_backend.Application.Users.Commands.ForgotPassword;

// Input validation compensating for the endpoint being anonymous (ADR-0016 §1): reject an empty or
// malformed email before it reaches the Keycloak admin lookup. Whether the address is actually
// registered is never revealed — that stays the handler's silent decision.
public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);
    }
}
