using FluentValidation.Results;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Handlers;

using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

public sealed class AssignUserRoleCommandHandler : IRequestHandler<AssignUserRoleCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;

    public AssignUserRoleCommandHandler(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        EnsureActorCanAssignRole(actor);

        if (!TryParseRole(request.Role, out var newRole))
        {
            throw CreateUnknownRoleValidationException();
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.AssignRole(newRole);

        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    private void EnsureActorCanAssignRole(User actor)
    {
        var decision = _accessPolicy.Evaluate(actor, ProtectedCapability.AdministratorPanel);

        if (!actor.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(actor.Id);
        }

        if (!decision.IsAllowed)
        {
            throw new UserRoleNotAuthorizedException(actor.Role, ProtectedCapability.AdministratorPanel);
        }
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
