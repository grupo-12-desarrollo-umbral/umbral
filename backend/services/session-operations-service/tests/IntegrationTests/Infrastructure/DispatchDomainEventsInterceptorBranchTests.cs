using MediatR;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

public sealed class DispatchDomainEventsInterceptorBranchTests
{
    [Fact]
    public async Task SavingChangesAsync_WhenContextIsNull_DoesNotThrow()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        var eventData = new DbContextEventData(null!, null!, context: null);
        var result = default(InterceptionResult<int>);

        _ = await interceptor.SavingChangesAsync(eventData, result, CancellationToken.None);
    }

    [Fact]
    public async Task SavedChangesAsync_WhenContextIsNull_DoesNotThrow()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: null, entitiesSavedCount: 0);
        var result = 0;

        _ = await interceptor.SavedChangesAsync(eventData, result, CancellationToken.None);
    }

    [Fact]
    public async Task SavedChangesAsync_WhenEntitiesHaveDomainEvents_PublishesViaMediator()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        var domainEvent = new TestDomainEvent();
        var entity = new TestEntity();
        entity.AddDomainEvent(domainEvent);

        var context = new Mock<DbContext>();
        var entry = new Mock<EntityEntry<BaseEntity>>();
        entry.Setup(e => e.Entity).Returns(entity);

        var entries = new List<EntityEntry<BaseEntity>> { entry.Object };
        context.Setup(c => c.ChangeTracker.Entries<BaseEntity>())
            .Returns(entries);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: context.Object, entitiesSavedCount: 0);

        await interceptor.SavedChangesAsync(eventData, 0, CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.Is<object>(o => o == domainEvent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavedChangesAsync_WhenEntitiesHaveNoDomainEvents_DoesNotPublish()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        var entity = new TestEntity();

        var context = new Mock<DbContext>();
        var entry = new Mock<EntityEntry<BaseEntity>>();
        entry.Setup(e => e.Entity).Returns(entity);

        var entries = new List<EntityEntry<BaseEntity>> { entry.Object };
        context.Setup(c => c.ChangeTracker.Entries<BaseEntity>())
            .Returns(entries);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: context.Object, entitiesSavedCount: 0);

        await interceptor.SavedChangesAsync(eventData, 0, CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class TestDomainEvent : BaseEvent { }

    private sealed class TestEntity : BaseEntity { }
}
