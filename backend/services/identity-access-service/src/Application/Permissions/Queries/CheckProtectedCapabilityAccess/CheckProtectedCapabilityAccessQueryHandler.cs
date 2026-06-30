using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

public sealed class CheckProtectedCapabilityAccessQueryHandler : IRequestHandler<CheckProtectedCapabilityAccessQuery, ProtectedAccessDecisionDto>
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public CheckProtectedCapabilityAccessQueryHandler(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task<ProtectedAccessDecisionDto> Handle(
        CheckProtectedCapabilityAccessQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _currentActor.GetActorAsync(cancellationToken);

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
