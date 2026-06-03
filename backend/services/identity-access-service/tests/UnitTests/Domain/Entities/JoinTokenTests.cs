using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class JoinTokenTests
{
    private readonly JoinTokenPolicy _policy = new();

    [Fact]
    public void Issue_WithValidValues_CreatesActiveTokenAndRaisesIssuedEvent()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var expiresAt = issuedAt.AddMinutes(15);
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var joinToken = JoinToken.Issue(liveSessionId, teamId, " token-hash ", issuedAt, expiresAt, 42, _policy);

        joinToken.JoinTokenId.Should().NotBe(Guid.Empty);
        joinToken.LiveSessionId.Should().Be(liveSessionId);
        joinToken.TeamId.Should().Be(teamId);
        joinToken.TokenHash.Should().Be("token-hash");
        joinToken.IssuedAt.Should().Be(issuedAt);
        joinToken.ExpiresAt.Should().Be(expiresAt);
        joinToken.ConsumedAt.Should().BeNull();
        joinToken.IssuedByUserId.Should().Be(42);
        joinToken.Status.Should().Be(JoinTokenStatus.Active);

        var domainEvent = joinToken.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JoinTokenIssuedEvent>().Subject;
        domainEvent.JoinTokenId.Should().Be(joinToken.JoinTokenId);
        domainEvent.LiveSessionId.Should().Be(liveSessionId);
        domainEvent.TeamId.Should().Be(teamId);
        domainEvent.IssuedAt.Should().Be(issuedAt);
        domainEvent.ExpiresAt.Should().Be(expiresAt);
        domainEvent.IssuedByUserId.Should().Be(42);
    }

    [Fact]
    public void Issue_WithInvalidValues_ThrowsExpectedExceptions()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var expiresAt = issuedAt.AddMinutes(15);

        FluentActions.Invoking(() => JoinToken.Issue(Guid.Empty, Guid.NewGuid(), "hash", issuedAt, expiresAt, 7, _policy))
            .Should().Throw<JoinTokenLiveSessionRequiredException>();
        FluentActions.Invoking(() => JoinToken.Issue(Guid.NewGuid(), Guid.Empty, "hash", issuedAt, expiresAt, 7, _policy))
            .Should().Throw<JoinTokenTeamRequiredException>();
        FluentActions.Invoking(() => JoinToken.Issue(Guid.NewGuid(), Guid.NewGuid(), " ", issuedAt, expiresAt, 7, _policy))
            .Should().Throw<JoinTokenHashRequiredException>();
        FluentActions.Invoking(() => JoinToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", issuedAt, expiresAt, 0, _policy))
            .Should().Throw<JoinTokenIssuerRequiredException>();
    }

    [Fact]
    public void Consume_OnActiveToken_SetsConsumedStateAndRaisesEvent()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var consumedAt = issuedAt.AddMinutes(2);
        var joinToken = JoinToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", issuedAt, issuedAt.AddMinutes(15), 9, _policy);
        joinToken.ClearDomainEvents();

        joinToken.Consume(consumedAt, _policy);

        joinToken.ConsumedAt.Should().Be(consumedAt);
        joinToken.Status.Should().Be(JoinTokenStatus.Consumed);

        var domainEvent = joinToken.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JoinTokenConsumedEvent>().Subject;
        domainEvent.JoinTokenId.Should().Be(joinToken.JoinTokenId);
        domainEvent.LiveSessionId.Should().Be(joinToken.LiveSessionId);
        domainEvent.TeamId.Should().Be(joinToken.TeamId);
        domainEvent.ConsumedAt.Should().Be(consumedAt);
    }

    [Fact]
    public void Consume_OnConsumedToken_ThrowsReplayRejectedException()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var joinToken = JoinToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", issuedAt, issuedAt.AddMinutes(15), 9, _policy);
        joinToken.Consume(issuedAt.AddMinutes(1), _policy);
        joinToken.ClearDomainEvents();

        FluentActions.Invoking(() => joinToken.Consume(issuedAt.AddMinutes(2), _policy))
            .Should().Throw<JoinTokenReplayRejectedException>();

        joinToken.DomainEvents.Should().BeEmpty();
    }
}
