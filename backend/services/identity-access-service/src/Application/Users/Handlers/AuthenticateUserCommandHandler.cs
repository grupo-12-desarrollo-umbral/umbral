using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Permissions.DTOs;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Handlers;

public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticateUserResultDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IdentityProvisioningPolicy _identityProvisioningPolicy;
    private readonly AccessPolicy _accessPolicy;

    public AuthenticateUserCommandHandler(
        IUserRepository userRepository,
        IdentityProvisioningPolicy identityProvisioningPolicy,
        AccessPolicy accessPolicy)
    {
        _userRepository = userRepository;
        _identityProvisioningPolicy = identityProvisioningPolicy;
        _accessPolicy = accessPolicy;
    }

    public async Task<AuthenticateUserResultDto> Handle(
        AuthenticateUserCommand request,
        CancellationToken cancellationToken)
    {
        var externalIdentityId = request.ExternalIdentityId.Trim();
        var role = GatewayRoleParser.Parse(request.Role);
        var existingUser = await _userRepository.GetByExternalIdentityIdAsync(externalIdentityId, cancellationToken);

        var user = _identityProvisioningPolicy.SynchronizeOrCreate(
            existingUser,
            externalIdentityId,
            request.DisplayName,
            request.Email,
            role);

        var accessDecision = _accessPolicy.Evaluate(user, ProtectedCapability.AuthenticatedPlatformAccess);

        EnsureAccess(user, accessDecision.IsAllowed);

        if (existingUser is null)
        {
            await _userRepository.AddAsync(user, cancellationToken);
        }
        else
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        return new AuthenticateUserResultDto(
            MapProfile(user),
            new ProtectedAccessDecisionDto(
                accessDecision.Capability.ToString(),
                accessDecision.IsAllowed,
                accessDecision.Reason));
    }

    private static AuthenticatedActorProfileDto MapProfile(User user)
    {
        return new AuthenticatedActorProfileDto(
            user.ExternalIdentityId,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            user.IsActive);
    }

    private static void EnsureAccess(User user, bool isAllowed)
    {
        if (!user.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(user.Id);
        }

        if (!isAllowed)
        {
            throw new UserRoleNotAuthorizedException(user.Role, ProtectedCapability.AuthenticatedPlatformAccess);
        }
    }
}
