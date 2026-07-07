namespace umbral_backend.Infrastructure.Identity.Keycloak;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string AdminAuthority { get; set; } = "http://keycloak:8080";
    public string Realm { get; set; } = "umbral";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin";

    // Bounded in-process retry for the Keycloak-first role sync. Absorbs transient blips
    // (token endpoint hiccup, brief partition) before surfacing a 503 to the admin caller.
    public int SyncMaxAttempts { get; set; } = 3;
    public int SyncRetryBaseDelayMs { get; set; } = 200;
}
