using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public sealed class JoinTokenPolicyTests
{
    private readonly JoinTokenPolicy _policy = new();

    [Fact]
    public void EnsureCanIssue_WithInvalidExpiration_ThrowsException()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);

        FluentActions.Invoking(() => _policy.EnsureCanIssue(issuedAt, issuedAt))
            .Should().Throw<JoinTokenExpirationInvalidException>();
    }

    [Fact]
    public void EnsureIsValid_WhenTokenHasExpired_ThrowsExpiredException()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var joinToken = JoinToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", issuedAt, issuedAt.AddMinutes(5), 7, _policy);

        FluentActions.Invoking(() => _policy.EnsureIsValid(joinToken, issuedAt.AddMinutes(6)))
            .Should().Throw<JoinTokenExpiredException>();
    }

    [Fact]
    public void EnsureIsValid_WhenTokenWasConsumed_RejectsReplay()
    {
        var issuedAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var joinToken = JoinToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", issuedAt, issuedAt.AddMinutes(5), 7, _policy);
        joinToken.Consume(issuedAt.AddMinutes(1), _policy);

        FluentActions.Invoking(() => _policy.EnsureIsValid(joinToken, issuedAt.AddMinutes(2)))
            .Should().Throw<JoinTokenReplayRejectedException>();
    }
}
