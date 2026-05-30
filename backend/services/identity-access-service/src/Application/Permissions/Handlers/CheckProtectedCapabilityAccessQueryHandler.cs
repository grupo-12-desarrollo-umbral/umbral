using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Permissions.DTOs;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Permissions.Handlers;

public sealed class CheckProtectedCapabilityAccessQueryHandler : IRequestHandler<CheckProtectedCapabilityAccessQuery, ProtectedAccessDecisionDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;

    public CheckProtectedCapabilityAccessQueryHandler(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<ProtectedAccessDecisionDto> Handle(
        CheckProtectedCapabilityAccessQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        var decision = _accessPolicy.Evaluate(user, request.Capability);

        EnsureAccess(user, decision.IsAllowed, request.Capability);

        return new ProtectedAccessDecisionDto(
            decision.Capability.ToString(),
            decision.IsAllowed,
            decision.Reason);
    }

    private static void EnsureAccess(User user, bool isAllowed, Domain.Enums.ProtectedCapability capability)
    {
        if (!user.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(user.Id);
        }

        if (!isAllowed)
        {
            throw new UserRoleNotAuthorizedException(user.Role, capability);
        }
    }
}
