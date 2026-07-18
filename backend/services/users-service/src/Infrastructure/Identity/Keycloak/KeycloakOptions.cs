using System.ComponentModel.DataAnnotations;

namespace umbral_backend.Infrastructure.Identity.Keycloak;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    // Dev defaults mirror the docker-compose Keycloak (the umbral-backend client seeded by the
    // realm import) so Development runs with no config. KeycloakOptionsValidator rejects these
    // outside Development, so a missing/partial config section can't silently authenticate against
    // Keycloak with the dev service-account secret.
    internal const string DevAdminAuthority = "http://keycloak:8080";
    internal const string DevRealm = "umbral";
    internal const string DevClientId = "umbral-backend";
    internal const string DevClientSecret = "umbral-backend-dev-secret";

    [Required]
    public string AdminAuthority { get; set; } = DevAdminAuthority;

    [Required]
    public string Realm { get; set; } = DevRealm;

    // Confidential client authenticated via client-credentials against the umbral realm; its
    // service account holds only the realm-management roles the Admin API calls need.
    [Required]
    public string ClientId { get; set; } = DevClientId;

    [Required]
    public string ClientSecret { get; set; } = DevClientSecret;

    // Bounded in-process retry for the Keycloak-first role sync. Absorbs transient blips
    // (token endpoint hiccup, brief partition) before surfacing a 503 to the admin caller.
    public int SyncMaxAttempts { get; set; } = 3;
    public int SyncRetryBaseDelayMs { get; set; } = 200;
}
