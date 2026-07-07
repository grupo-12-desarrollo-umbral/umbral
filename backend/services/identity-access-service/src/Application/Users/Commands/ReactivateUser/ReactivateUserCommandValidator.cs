namespace umbral_backend.Application.Users.Commands.ReactivateUser;

public sealed class ReactivateUserCommandValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .GreaterThan(0);
    }
}
