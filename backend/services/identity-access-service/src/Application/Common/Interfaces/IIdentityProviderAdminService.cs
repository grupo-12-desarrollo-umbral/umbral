using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Interfaces;

public interface IIdentityProviderAdminService
{
    Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken);
}
