using umbral_backend.Domain.Common;

namespace umbral_backend.ScoringMonitoring.UnitTests.Common;

// Covers the BaseEntity domain-event buffer (add/remove/clear + read-only projection) and the
// BaseAuditableEntity audit columns, exercised through minimal test doubles.
public sealed class BaseEntityTests
{
    private sealed class SampleEntity : BaseAuditableEntity;

    private sealed class SampleEvent : BaseEvent;

    [Fact]
    public void AddDomainEvent_BuffersEvent()
    {
        var entity = new SampleEntity();
        var domainEvent = new SampleEvent();

        entity.AddDomainEvent(domainEvent);

        entity.DomainEvents.Should().ContainSingle().Which.Should().BeSameAs(domainEvent);
    }

    [Fact]
    public void RemoveDomainEvent_DropsEvent()
    {
        var entity = new SampleEntity();
        var domainEvent = new SampleEvent();
        entity.AddDomainEvent(domainEvent);

        entity.RemoveDomainEvent(domainEvent);

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_EmptiesBuffer()
    {
        var entity = new SampleEntity();
        entity.AddDomainEvent(new SampleEvent());
        entity.AddDomainEvent(new SampleEvent());

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AuditableColumns_RoundTrip()
    {
        var created = DateTimeOffset.UtcNow;
        var modified = created.AddMinutes(5);
        var entity = new SampleEntity
        {
            Id = 7,
            Created = created,
            CreatedBy = "creator",
            LastModified = modified,
            LastModifiedBy = "editor"
        };

        entity.Id.Should().Be(7);
        entity.Created.Should().Be(created);
        entity.CreatedBy.Should().Be("creator");
        entity.LastModified.Should().Be(modified);
        entity.LastModifiedBy.Should().Be("editor");
    }
}
