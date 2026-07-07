using System.ComponentModel.DataAnnotations;

namespace umbral_backend.Infrastructure.Identity.Keycloak;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    // Dev defaults mirror the docker-compose Keycloak so Development runs with no config.
    // KeycloakOptionsValidator rejects these outside Development, so a missing/partial config
    // section can't silently authenticate against Keycloak with dev credentials.
    internal const string DevAdminAuthority = "http://keycloak:8080";
    internal const string DevRealm = "umbral";
    internal const string DevAdminUsername = "admin";
    internal const string DevAdminPassword = "admin";

    [Required]
    public string AdminAuthority { get; set; } = DevAdminAuthority;

    [Required]
    public string Realm { get; set; } = DevRealm;

    [Required]
    public string AdminUsername { get; set; } = DevAdminUsername;

    [Required]
    public string AdminPassword { get; set; } = DevAdminPassword;

    // Bounded in-process retry for the Keycloak-first role sync. Absorbs transient blips
    // (token endpoint hiccup, brief partition) before surfacing a 503 to the admin caller.
    public int SyncMaxAttempts { get; set; } = 3;
    public int SyncRetryBaseDelayMs { get; set; } = 200;
}
