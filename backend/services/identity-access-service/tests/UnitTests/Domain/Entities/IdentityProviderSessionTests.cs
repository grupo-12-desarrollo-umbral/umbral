using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class IdentityProviderSessionTests
{
    [Fact]
    public void Start_WithValidValues_CreatesSessionAndRaisesStartedEvent()
    {
        var startedAt = new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero);
        var expiresAt = startedAt.AddHours(1);

        var session = IdentityProviderSession.Start(7, " Keycloak ", " sid-01 ", startedAt, expiresAt);

        session.UserId.Should().Be(7);
        session.ProviderName.Should().Be("Keycloak");
        session.ProviderSessionKey.Should().Be("sid-01");
        session.StartedAt.Should().Be(startedAt);
        session.ExpiresAt.Should().Be(expiresAt);
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IdentityProviderSessionStartedEvent>();
    }

    [Fact]
    public void End_FirstTime_RevokesSessionAndRaisesEndedEvent()
    {
        var session = IdentityProviderSession.Start(7, "Keycloak", "sid-02", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));
        session.ClearDomainEvents();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(10);

        session.End(endedAt);

        session.RevokedAt.Should().Be(endedAt);
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IdentityProviderSessionEndedEvent>();
    }

    [Fact]
    public void End_WhenAlreadyRevoked_DoesNothing()
    {
        var session = IdentityProviderSession.Start(7, "Keycloak", "sid-03", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));
        var firstEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        session.End(firstEnd);
        session.ClearDomainEvents();

        session.End(DateTimeOffset.UtcNow.AddMinutes(15));

        session.RevokedAt.Should().Be(firstEnd);
        session.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Start_WithInvalidValues_ThrowsExpectedExceptions()
    {
        var startedAt = new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero);

        FluentActions.Invoking(() => IdentityProviderSession.Start(0, "Keycloak", "sid", startedAt, startedAt.AddHours(1)))
            .Should().Throw<IdentityProviderSessionUserRequiredException>();
        FluentActions.Invoking(() => IdentityProviderSession.Start(1, " ", "sid", startedAt, startedAt.AddHours(1)))
            .Should().Throw<IdentityProviderNameRequiredException>();
        FluentActions.Invoking(() => IdentityProviderSession.Start(1, "Keycloak", " ", startedAt, startedAt.AddHours(1)))
            .Should().Throw<IdentityProviderSessionKeyRequiredException>();
        FluentActions.Invoking(() => IdentityProviderSession.Start(1, "Keycloak", "sid", startedAt, startedAt))
            .Should().Throw<IdentityProviderSessionExpirationInvalidException>();
    }
}
