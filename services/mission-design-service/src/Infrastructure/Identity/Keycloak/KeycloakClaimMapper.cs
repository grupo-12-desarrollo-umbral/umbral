using System.Security.Claims;

namespace umbral_backend.Infrastructure.Identity.Keycloak;

public static class KeycloakClaimMapper
{
    public static string MapToRole(string keycloakRole) => keycloakRole;
    public static string MapToClaim(string keycloakClaim) => keycloakClaim;
}
