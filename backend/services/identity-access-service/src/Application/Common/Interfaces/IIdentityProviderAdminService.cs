using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Interfaces;

public interface IIdentityProviderAdminService
{
    Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken);

    // Toggle the identity provider account's enabled state. isActive=false disables it so a
    // deactivated user stops receiving fresh JWTs; isActive=true re-enables (reactivation).
    Task SyncUserActiveStateAsync(string externalIdentityId, bool isActive, CancellationToken cancellationToken);
}
