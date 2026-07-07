using Microsoft.Extensions.Options;

namespace umbral_backend.Infrastructure.Identity.Keycloak;

// Fails startup outside Development when any Keycloak setting is unset or still on its dev default,
// so a missing/partial config section can't silently authenticate with the docker-compose credentials.
public sealed class KeycloakOptionsValidator : IValidateOptions<KeycloakOptions>
{
    private readonly bool _isDevelopment;

    public KeycloakOptionsValidator(bool isDevelopment) => _isDevelopment = isDevelopment;

    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        if (_isDevelopment)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        Check(errors, nameof(options.AdminAuthority), options.AdminAuthority, KeycloakOptions.DevAdminAuthority);
        Check(errors, nameof(options.Realm), options.Realm, KeycloakOptions.DevRealm);
        Check(errors, nameof(options.AdminUsername), options.AdminUsername, KeycloakOptions.DevAdminUsername);
        Check(errors, nameof(options.AdminPassword), options.AdminPassword, KeycloakOptions.DevAdminPassword);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void Check(List<string> errors, string field, string? value, string devDefault)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Keycloak:{field} must be configured outside Development.");
        }
        else if (string.Equals(value, devDefault, StringComparison.Ordinal))
        {
            errors.Add($"Keycloak:{field} is still set to its development default and must be overridden outside Development.");
        }
    }
}
