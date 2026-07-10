using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class JoinContextTests
{
    [Fact]
    public void Create_WithInvalidExpiration_ThrowsException()
    {
        var now = DateTimeOffset.UtcNow;

        var act = () => JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now);

        act.Should().Throw<JoinContextExpirationInvalidException>();
    }

    [Fact]
    public void Consume_MarksContextConsumed()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));

        joinContext.Consume(now.AddMinutes(1));

        joinContext.Status.Should().Be(JoinContextStatus.Consumed);
        joinContext.ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_AfterConsume_ThrowsException()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));
        joinContext.Consume(now.AddMinutes(1));

        var act = () => joinContext.Cancel();

        act.Should().Throw<JoinContextAlreadyClosedException>();
    }

    [Fact]
    public void Consume_AfterExpiration_MarksContextExpiredAndThrowsException()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));

        var act = () => joinContext.Consume(now.AddMinutes(6));

        act.Should().Throw<JoinContextAlreadyClosedException>();
        joinContext.Status.Should().Be(JoinContextStatus.Expired);
    }

    [Fact]
    public void Cancel_WhenPending_MarksContextCancelled()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));

        joinContext.Cancel();

        joinContext.Status.Should().Be(JoinContextStatus.Cancelled);
    }

    [Fact]
    public void Expire_BeforeExpiration_ThrowsException()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));

        var act = () => joinContext.Expire(now.AddMinutes(4));

        act.Should().Throw<JoinContextExpirationInvalidException>();
    }

    [Fact]
    public void Expire_AfterConsumption_ThrowsException()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));
        joinContext.Consume(now.AddMinutes(1));

        var act = () => joinContext.Expire(now.AddMinutes(6));

        act.Should().Throw<JoinContextAlreadyClosedException>();
    }

    [Fact]
    public void Expire_AtOrAfterExpiresAt_MarksExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var joinContext = JoinContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(5));

        // occurredAt >= ExpiresAt → the valid arm of the expiration guard.
        joinContext.Expire(now.AddMinutes(5));

        joinContext.Status.Should().Be(JoinContextStatus.Expired);
    }
}
