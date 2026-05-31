using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Interfaces;

public interface IKeycloakAdminService
{
    Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken);
}
