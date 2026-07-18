namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandValidator : AbstractValidator<AuthenticateUserCommand>
{
    public AuthenticateUserCommandValidator()
    {
        RuleFor(command => command.DisplayName)
            .NotEmpty();
    }
}
