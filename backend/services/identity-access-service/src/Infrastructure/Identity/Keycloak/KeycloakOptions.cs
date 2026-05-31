namespace umbral_backend.Infrastructure.Identity.Keycloak;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string AdminAuthority { get; set; } = "http://keycloak:8080";
    public string Realm { get; set; } = "umbral";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin";
}
