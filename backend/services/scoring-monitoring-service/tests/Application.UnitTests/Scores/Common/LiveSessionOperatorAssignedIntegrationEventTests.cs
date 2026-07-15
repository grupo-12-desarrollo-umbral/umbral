using MassTransit;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Application.UnitTests.Scores.Common;

public sealed class LiveSessionOperatorAssignedIntegrationEventTests
{
    private static LiveSessionOperatorAssignedIntegrationEvent Sample(Guid sessionId) => new(
        LiveSessionId: sessionId,
        AssignedOperatorUserId: Guid.NewGuid(),
        AssignedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public void Contract_BindsToSessionOperatorAssignedExchange()
    {
        var entityName = typeof(LiveSessionOperatorAssignedIntegrationEvent)
            .GetCustomAttributes(typeof(EntityNameAttribute), inherit: false)
            .Cast<EntityNameAttribute>()
            .Single();

        entityName.EntityName.Should().Be("session-operator-assigned");
    }

    [Fact]
    public void IntegrationEvent_ValueEquality_HoldsAndDiffers()
    {
        var sessionId = Guid.NewGuid();
        var first = Sample(sessionId);
        var second = first with { };
        var different = first with { LiveSessionId = Guid.NewGuid() };

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.Should().NotBe(different);
        first.ToString().Should().Contain(nameof(LiveSessionOperatorAssignedIntegrationEvent.LiveSessionId));
    }
}
