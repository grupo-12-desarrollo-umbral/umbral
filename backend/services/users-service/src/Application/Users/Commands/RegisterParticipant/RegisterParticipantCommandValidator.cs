namespace umbral_backend.Application.Users.Commands.RegisterParticipant;

// Input validation compensating for the endpoint being anonymous (ADR-0016 §1): reject obviously
// malformed input before it reaches the Keycloak admin API. Password strength is deliberately NOT
// duplicated here — Keycloak's realm password policy stays the sole source of truth; this only rejects
// an empty/absurd password so a blank credential never reaches the create-user call.
public sealed class RegisterParticipantCommandValidator : AbstractValidator<RegisterParticipantCommand>
{
    public RegisterParticipantCommandValidator()
    {
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            // Fits the User.DisplayName column (max 200) provisioned on first sign-in.
            .MaximumLength(200);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);

        RuleFor(command => command.Password)
            .NotEmpty()
            // A floor only; the realm policy owns real strength rules and rejects a weak password on
            // the create-user call, surfacing Keycloak's own message.
            .MinimumLength(8)
            .MaximumLength(256);
    }
}
