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
            AdminUsername = "svc-admin",
            AdminPassword = "s3cr3t-not-admin",
        };

        var result = validator.Validate(name: null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle()
            .Which.Should().Contain("must be configured");
    }
}
