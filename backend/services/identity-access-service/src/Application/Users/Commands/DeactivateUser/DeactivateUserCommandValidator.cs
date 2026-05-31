namespace umbral_backend.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .GreaterThan(0);
    }
}
