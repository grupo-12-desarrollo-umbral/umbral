using umbral_backend.Domain.Common;

namespace umbral_backend.Application.UnitTests.Domain.Common;

public sealed class BaseEntityTests
{
    [Fact]
    public void DomainEventMethods_AddRemoveAndClearEvents()
    {
        var entity = new TestEntity();
        var first = new TestEvent();
        var second = new TestEvent();

        entity.AddDomainEvent(first);
        entity.AddDomainEvent(second);
        entity.DomainEvents.Should().HaveCount(2);

        entity.RemoveDomainEvent(first);
        entity.DomainEvents.Should().ContainSingle().Which.Should().BeSameAs(second);

        entity.ClearDomainEvents();
        entity.DomainEvents.Should().BeEmpty();
    }

    private sealed class TestEntity : BaseEntity;

    private sealed class TestEvent : BaseEvent;
}
