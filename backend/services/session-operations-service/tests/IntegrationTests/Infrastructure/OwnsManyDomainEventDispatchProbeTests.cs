using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

// Targeted probe: verifies that DispatchDomainEventsInterceptor collects domain events
// from EF Core owned-entity entries (OwnsMany), not just directly-attached DbSet entities.
// Converts the Stop-1 assumption into a green check before HU-32's outcome events are
// raised on owned EvidenceSubmission children.
public sealed class OwnsManyDomainEventDispatchProbeTests
{
    [Fact]
    public async Task SavedChangesAsync_WhenOwnedChildRaisesDomainEvent_ReachesDispatcherExactlyOnce()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        await using var context = NewContext();
        var parent = new ParentEntity();
        parent.AddChild(new ChildEntity());
        context.Add(parent);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: context, entitiesSavedCount: 0);

        await interceptor.SavedChangesAsync(eventData, 0, CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.IsAny<BaseEvent>(),
            It.IsAny<CancellationToken>()), Times.Once,
            "Owned-entity domain event was not collected by the interceptor");
    }

    [Fact]
    public async Task SavedChangesAsync_WhenMultipleOwnedChildrenRaiseEvents_EachReachesDispatcherOnce()
    {
        var mediator = new Mock<IMediator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var interceptor = new DispatchDomainEventsInterceptor(mediator.Object, serviceProvider.Object);

        await using var context = NewContext();
        var parent = new ParentEntity();
        parent.AddChild(new ChildEntity());
        parent.AddChild(new ChildEntity());
        parent.AddChild(new ChildEntity());
        context.Add(parent);

        var eventData = new SaveChangesCompletedEventData(null!, null!, context: context, entitiesSavedCount: 0);

        await interceptor.SavedChangesAsync(eventData, 0, CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.IsAny<BaseEvent>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private static OwnsManyProbeDbContext NewContext()
    {
        return new OwnsManyProbeDbContext(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private sealed class OwnsManyProbeDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ParentEntity> Parents => Set<ParentEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParentEntity>(parent =>
            {
                parent.OwnsMany(p => p.Children, child =>
                {
                    child.WithOwner().HasForeignKey("ParentId");
                    child.HasKey("Id");
                });
            });
        }
    }

    private sealed class ChildDomainEvent : BaseEvent { }

    private sealed class ParentEntity : BaseEntity
    {
        public List<ChildEntity> Children { get; } = new();

        public void AddChild(ChildEntity child)
        {
            Children.Add(child);
        }
    }

    private sealed class ChildEntity : BaseEntity
    {
        public ChildEntity()
        {
            AddDomainEvent(new ChildDomainEvent());
        }
    }
}
