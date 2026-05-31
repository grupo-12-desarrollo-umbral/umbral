using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class UserRoleNotAuthorizedException : Exception
{
    public UserRoleNotAuthorizedException(Role role, ProtectedCapability capability)
        : base($"Role '{role}' is not authorized to access '{capability}'.")
    {
    }
}
