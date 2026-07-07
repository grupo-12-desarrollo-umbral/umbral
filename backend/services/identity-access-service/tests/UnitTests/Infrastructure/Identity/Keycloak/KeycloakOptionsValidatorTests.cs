using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using umbral_backend.Infrastructure.Identity.Keycloak;

namespace umbral_backend.Application.UnitTests.Infrastructure.Identity.Keycloak;

// Guards the non-Development gate on Keycloak config: dev defaults (i.e. a missing config section)
// must fail startup outside Development, and stay allowed inside it.
public sealed class KeycloakOptionsValidatorTests
{
    private static KeycloakOptions DevDefaults() => new();

    private static KeycloakOptions ProductionConfig() => new()
    {
        AdminAuthority = "https://keycloak.prod.example.com",
        Realm = "umbral-prod",
        AdminUsername = "svc-admin",
        AdminPassword = "s3cr3t-not-admin",
    };

    [Fact]
    public void Validate_NonDevelopmentWithDevDefaults_FailsForEveryField()
    {
        var validator = new KeycloakOptionsValidator(isDevelopment: false);

        var result = validator.Validate(name: null, DevDefaults());

        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCount(4);
    }

    [Fact]
    public void Validate_DevelopmentWithDevDefaults_Succeeds()
    {
        var validator = new KeycloakOptionsValidator(isDevelopment: true);

        validator.Validate(name: null, DevDefaults()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonDevelopmentWithOverriddenConfig_Succeeds()
    {
        var validator = new KeycloakOptionsValidator(isDevelopment: false);

        validator.Validate(name: null, ProductionConfig()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateOnStart_ProductionWithMissingConfig_ThrowsAtStartup()
    {
        // No configuration bound => options keep dev defaults, mirroring a missing "Keycloak" section.
        using var provider = BuildProvider(isDevelopment: false);
        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        var act = startupValidator.Validate;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void ValidateOnStart_DevelopmentWithMissingConfig_DoesNotThrow()
    {
        using var provider = BuildProvider(isDevelopment: true);
        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        var act = startupValidator.Validate;

        act.Should().NotThrow();
    }

    private static ServiceProvider BuildProvider(bool isDevelopment)
    {
        var services = new ServiceCollection();
        services.AddOptions<KeycloakOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<KeycloakOptions>>(new KeycloakOptionsValidator(isDevelopment));
        return services.BuildServiceProvider();
    }
}
