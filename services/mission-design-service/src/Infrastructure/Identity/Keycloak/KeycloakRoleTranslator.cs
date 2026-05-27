namespace umbral_backend.Infrastructure.Identity.Keycloak;

public class KeycloakRoleTranslator
{
    public string Translate(string keycloakRole) => keycloakRole switch
    {
        "admin" => "Administrator",
        "user" => "User",
        _ => keycloakRole
    };
}
