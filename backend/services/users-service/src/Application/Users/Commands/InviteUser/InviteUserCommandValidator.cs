namespace umbral_backend.Application.Users.Commands.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            // Capped so the address still fits the User.DisplayName column (max 200) when it stands
            // in as the pending record's placeholder display name; 200 comfortably covers any real
            // address.
            .MaximumLength(200);

        RuleFor(command => command.Role)
            .NotEmpty()
            .Must(BeKnownRole)
            .WithMessage("Role must be a known Role value.");

        // The "Participant cannot be invited" rule is enforced in the domain (User.EnsureInvitableRole
        // → ParticipantNotInvitableException, 422), so it is not duplicated here.
    }

    private static bool BeKnownRole(string role)
    {
        return Enum.TryParse<Domain.Enums.Role>(role, ignoreCase: true, out var parsedRole)
            && Enum.IsDefined(parsedRole);
    }
}
