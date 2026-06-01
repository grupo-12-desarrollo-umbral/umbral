using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

public sealed class AccessPolicy
{
    public AccessDecision Evaluate(User user, ProtectedCapability capability)
    {
        ArgumentNullException.ThrowIfNull(user);

        AccessDecision decision;

        if (!user.IsActive)
        {
            decision = AccessDecision.Deny(capability, "User is deactivated.");
            user.RecordAccessDecision(decision);
            return decision;
        }

        var isAllowed = capability switch
        {
            ProtectedCapability.AuthenticatedPlatformAccess => true,
            ProtectedCapability.AdministratorPanel => user.Role == Role.Administrator,
            ProtectedCapability.OperatorPanel => user.Role is Role.Administrator or Role.Operator,
            ProtectedCapability.ParticipantExperience => user.Role == Role.Participant,
            ProtectedCapability.UserAccessCatalog => user.Role == Role.Administrator,
            _ => false
        };

        decision = isAllowed
            ? AccessDecision.Allow(capability, "Access granted for role.")
            : AccessDecision.Deny(capability, "Role is not authorized for capability.");

        user.RecordAccessDecision(decision);
        return decision;
    }

    public void EnsureCanAccess(User user, ProtectedCapability capability)
    {
        var decision = Evaluate(user, capability);

        if (!user.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(user.Id);
        }

        if (!decision.IsAllowed)
        {
            throw new UserRoleNotAuthorizedException(user.Role, capability);
        }
    }
}
