using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public sealed class IdentityProvisioningPolicy
{
    public User SynchronizeOrCreate(
        User? existingUser,
        string externalIdentityId,
        string displayName,
        string email,
        Role role)
    {
        if (existingUser is null)
        {
            return User.Provision(externalIdentityId, displayName, email, role);
        }

        if (!string.Equals(existingUser.ExternalIdentityId, externalIdentityId.Trim(), StringComparison.Ordinal))
        {
            throw new ExternalIdentityMismatchException(existingUser.Id);
        }

        existingUser.SynchronizeProfile(displayName, email);

        return existingUser;
    }
}
