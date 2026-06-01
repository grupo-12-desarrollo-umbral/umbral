namespace umbral_backend.Application.Users.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandValidator : AbstractValidator<AuthenticateUserCommand>
{
    public AuthenticateUserCommandValidator()
    {
        RuleFor(command => command.ExternalIdentityId)
            .NotEmpty();

        RuleFor(command => command.DisplayName)
            .NotEmpty();

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
