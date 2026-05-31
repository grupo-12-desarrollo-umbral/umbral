using umbral_backend.Application.Common.Security;

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

        RuleFor(command => command.Role)
            .NotEmpty()
            .Must(role => GatewayRoleParser.TryParse(role, out _))
            .WithMessage("Role must be a supported gateway role.");
    }
}
