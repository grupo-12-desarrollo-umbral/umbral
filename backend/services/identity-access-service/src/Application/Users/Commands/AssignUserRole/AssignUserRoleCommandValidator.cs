using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Users.Commands.AssignUserRole;

public sealed class AssignUserRoleCommandValidator : AbstractValidator<AssignUserRoleCommand>
{
    private readonly IUserRepository _userRepository;

    public AssignUserRoleCommandValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;

        RuleFor(command => command.UserId)
            .GreaterThan(0);

        RuleFor(command => command.Role)
            .NotEmpty()
            .Must(BeKnownRole)
            .WithMessage("Role must be a known Role value.");

        RuleFor(command => command)
            .CustomAsync(ValidateTargetUserAsync);
    }

    private async Task ValidateTargetUserAsync(
        AssignUserRoleCommand command,
        ValidationContext<AssignUserRoleCommand> context,
        CancellationToken cancellationToken)
    {
        if (command.UserId <= 0)
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            context.AddFailure(nameof(AssignUserRoleCommand.UserId), "Target user must exist.");
            return;
        }

        if (!user.IsActive)
        {
            context.AddFailure(nameof(AssignUserRoleCommand.UserId), "Target user must be active.");
        }
    }

    private static bool BeKnownRole(string role)
    {
        return Enum.TryParse<Domain.Enums.Role>(role, ignoreCase: true, out var parsedRole)
            && Enum.IsDefined(parsedRole);
    }
}
