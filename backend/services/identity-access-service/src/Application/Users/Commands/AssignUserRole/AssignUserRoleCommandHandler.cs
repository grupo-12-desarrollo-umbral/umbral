using FluentValidation.Results;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Users.Commands.AssignUserRole;

using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

// Real subject wrapped by UserRoleAssignmentAuthorizationProxy (the registered IRequestHandler).
public sealed class AssignUserRoleCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderAdminService _identityProviderAdmin;

    public AssignUserRoleCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderAdminService identityProviderAdmin)
    {
        _userRepository = userRepository;
        _identityProviderAdmin = identityProviderAdmin;
    }

    public async Task Handle(AssignUserRoleCommand command, CancellationToken cancellationToken)
    {
        if (!TryParseRole(command.Role, out var newRole))
        {
            throw CreateUnknownRoleValidationException();
        }

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), command.UserId);

        user.AssignRole(newRole);

        await _userRepository.UpdateAsync(user, cancellationToken);

        await _identityProviderAdmin.SyncUserRoleAsync(user.ExternalIdentityId, newRole, cancellationToken);
    }

    private static bool TryParseRole(string role, out Role parsedRole)
    {
        return Enum.TryParse(role, ignoreCase: true, out parsedRole) && Enum.IsDefined(parsedRole);
    }

    private static ValidationException CreateUnknownRoleValidationException()
    {
        return new ValidationException(
            new[]
            {
                new ValidationFailure(nameof(AssignUserRoleCommand.Role), "Role must be a known Role value.")
            });
    }
}
