using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

// Drives DispatchDomainEventsInterceptor's post-commit fan-out over a real EF change tracker
// (in-memory provider) so it walks genuine EntityEntry<BaseEntity> instances — EntityEntry has no
// public ctor and cannot be mocked by Moq/Castle.
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

        await using var context = NewContext();
        context.Add(entity);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: context, entitiesSavedCount: 0);

        await interceptor.SavedChangesAsync(eventData, 0, CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.Is<BaseEvent>(o => o == domainEvent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavedChangesAsync_WhenEntitiesHaveNoDomainEvents_DoesNotPublish()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        var entity = new TestEntity();

        await using var context = NewContext();
        context.Add(entity);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: context, entitiesSavedCount: 0);

        await interceptor.SavedChangesAsync(eventData, 0, CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.IsAny<BaseEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TestDbContext NewContext()
    {
        return new TestDbContext(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();
    }

    private sealed class TestDomainEvent : BaseEvent { }

    private sealed class TestEntity : BaseEntity { }
}
