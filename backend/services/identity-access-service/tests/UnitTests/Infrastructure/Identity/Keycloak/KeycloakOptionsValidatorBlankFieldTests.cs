using umbral_backend.Infrastructure.Identity.Keycloak;

namespace umbral_backend.Application.UnitTests.Infrastructure.Identity.Keycloak;

// Covers the Check() IsNullOrWhiteSpace-true branch: a field left blank (as opposed to left on its
// non-empty dev default, which the existing tests exercise via the else-if arm).
public sealed class KeycloakOptionsValidatorBlankFieldTests
{
    [Fact]
    public void Validate_NonDevelopmentWithBlankField_ReportsMustBeConfigured()
    {
        var validator = new KeycloakOptionsValidator(isDevelopment: false);
        var options = new KeycloakOptions
        {
            AdminAuthority = "   ",                                  // blank → "must be configured"
            Realm = "umbral-prod",
            ClientId = "umbral-backend-prod",
            ClientSecret = "s3cr3t-not-the-dev-default",
        };

        var result = validator.Validate(name: null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle()
            .Which.Should().Contain("must be configured");
    }

    [Fact]
    public void Validate_NonDevelopmentWithPlaceholderClientSecret_RejectsIt()
    {
        // The dev-default secret is a placeholder that must never authenticate outside Development.
        var validator = new KeycloakOptionsValidator(isDevelopment: false);
        var options = new KeycloakOptions
        {
            AdminAuthority = "https://keycloak.prod.example.com",
            Realm = "umbral-prod",
            ClientId = "umbral-backend-prod",
            ClientSecret = KeycloakOptions.DevClientSecret,         // still the placeholder → rejected
        };

        var result = validator.Validate(name: null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle()
            .Which.Should().Contain("development default");
    }
}
