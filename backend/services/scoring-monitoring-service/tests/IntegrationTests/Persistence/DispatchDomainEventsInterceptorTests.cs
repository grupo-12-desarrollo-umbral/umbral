using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// Covers DispatchDomainEventsInterceptor: entities with buffered events are published and cleared
// after save (sync and async paths), entities without events publish nothing, and the null-context
// guard is honoured on both entry points. In-memory model, mocked IMediator — no broker/DB.
public sealed class DispatchDomainEventsInterceptorTests
{
    private sealed class Ping : BaseEvent;

    private sealed class EventThing : BaseEntity
    {
        public void Raise() => AddDomainEvent(new Ping());
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EventThing> Things => Set<EventThing>();
    }

    private static TestDbContext NewContext(DispatchDomainEventsInterceptor interceptor) =>
        new(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options);

    private static DispatchDomainEventsInterceptor NewInterceptor(
        IMediator mediator,
        Mock<IOutboxDomainEventDispatcher>? outboxDispatcher = null)
    {
        outboxDispatcher ??= new Mock<IOutboxDomainEventDispatcher>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IOutboxDomainEventDispatcher)))
            .Returns(outboxDispatcher.Object);

        return new DispatchDomainEventsInterceptor(mediator, serviceProvider.Object);
    }

    [Fact]
    public async Task SavedChangesAsync_EntityWithEvents_PublishesAndClears()
    {
        var mediator = new Mock<IMediator>();
        await using var context = NewContext(NewInterceptor(mediator.Object));
        var thing = new EventThing();
        thing.Raise();
        context.Add(thing);

        await context.SaveChangesAsync();

        mediator.Verify(m => m.Publish(It.IsAny<BaseEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        thing.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SavedChangesAsync_ForwardsTheSaveCancellationToken()
    {
        var mediator = new Mock<IMediator>();
        await using var context = NewContext(NewInterceptor(mediator.Object));
        var thing = new EventThing();
        thing.Raise();
        context.Add(thing);
        using var cts = new CancellationTokenSource();

        await context.SaveChangesAsync(cts.Token);

        mediator.Verify(m => m.Publish(It.IsAny<BaseEvent>(), cts.Token), Times.Once);
    }

    [Fact]
    public void SavedChanges_EntityWithEvents_PublishesOnSyncPath()
    {
        var mediator = new Mock<IMediator>();
        using var context = NewContext(NewInterceptor(mediator.Object));
        var thing = new EventThing();
        thing.Raise();
        context.Add(thing);

        context.SaveChanges();

        mediator.Verify(m => m.Publish(It.IsAny<BaseEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavedChangesAsync_EntityWithoutEvents_PublishesNothing()
    {
        var mediator = new Mock<IMediator>();
        await using var context = NewContext(NewInterceptor(mediator.Object));
        context.Add(new EventThing());

        await context.SaveChangesAsync();

        mediator.Verify(m => m.Publish(It.IsAny<BaseEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NullContext_IsIgnoredOnBothEntryPoints()
    {
        var mediator = new Mock<IMediator>();
        var interceptor = NewInterceptor(mediator.Object);
        var eventData = new SaveChangesCompletedEventData(null!, null!, context: null, entitiesSavedCount: 0);

        var sync = interceptor.SavedChanges(eventData, 0);
        var async = await interceptor.SavedChangesAsync(eventData, 0);

        sync.Should().Be(0);
        async.Should().Be(0);
        mediator.Verify(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SavingChangesAsync_EntityWithEvents_EnqueuesBeforePostCommitPublication()
    {
        var mediator = new Mock<IMediator>();
        var outboxDispatcher = new Mock<IOutboxDomainEventDispatcher>();
        await using var context = NewContext(NewInterceptor(mediator.Object, outboxDispatcher));
        var thing = new EventThing();
        thing.Raise();
        context.Add(thing);

        await context.SaveChangesAsync();

        outboxDispatcher.Verify(
            dispatcher => dispatcher.DispatchAsync(It.IsAny<Ping>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(
            dispatcher => dispatcher.Publish(It.IsAny<BaseEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
