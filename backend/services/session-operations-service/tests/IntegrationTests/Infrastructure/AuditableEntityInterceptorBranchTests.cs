using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

public sealed class AuditableEntityInterceptorBranchTests
{
    [Fact]
    public async Task SavingChangesAsync_WhenContextIsNull_DoesNotThrow()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Id).Returns("test-user");
        var interceptor = new AuditableEntityInterceptor(currentUser.Object, TimeProvider.System);

        var eventData = new DbContextEventData(null!, null!, context: null);
        var result = default(InterceptionResult<int>);

        _ = await interceptor.SavingChangesAsync(eventData, result, CancellationToken.None);
    }

    [Fact]
    public void SavingChanges_WhenContextIsNull_DoesNotThrow()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Id).Returns("test-user");
        var interceptor = new AuditableEntityInterceptor(currentUser.Object, TimeProvider.System);

        var eventData = new DbContextEventData(null!, null!, context: null);
        var result = default(InterceptionResult<int>);

        var act = () => interceptor.SavingChanges(eventData, result);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SavingChangesAsync_WhenEntityIsAdded_SetsCreatedAndLastModified()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Id).Returns("test-user");
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var interceptor = new AuditableEntityInterceptor(currentUser.Object, timeProvider);

        var entity = new TestAuditableEntity();
        var context = CreateMockContext(entity, EntityState.Added);

        var eventData = new DbContextEventData(null!, null!, context: context.Object);
        var result = default(InterceptionResult<int>);

        await interceptor.SavingChangesAsync(eventData, result, CancellationToken.None);

        entity.Created.Should().Be(timeProvider.GetUtcNow());
        entity.CreatedBy.Should().Be("test-user");
        entity.LastModified.Should().Be(timeProvider.GetUtcNow());
        entity.LastModifiedBy.Should().Be("test-user");
    }

    [Fact]
    public async Task SavingChangesAsync_WhenEntityIsModified_SetsLastModifiedOnly()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Id).Returns("test-user");
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var interceptor = new AuditableEntityInterceptor(currentUser.Object, timeProvider);

        var entity = new TestAuditableEntity
        {
            Created = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "original-user"
        };
        var context = CreateMockContext(entity, EntityState.Modified);

        var eventData = new DbContextEventData(null!, null!, context: context.Object);
        var result = default(InterceptionResult<int>);

        await interceptor.SavingChangesAsync(eventData, result, CancellationToken.None);

        entity.Created.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        entity.CreatedBy.Should().Be("original-user");
        entity.LastModified.Should().Be(timeProvider.GetUtcNow());
        entity.LastModifiedBy.Should().Be("test-user");
    }

    [Fact]
    public async Task SavingChangesAsync_WhenEntityIsUnchanged_SkipsUpdate()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Id).Returns("test-user");
        var interceptor = new AuditableEntityInterceptor(currentUser.Object, TimeProvider.System);

        var entity = new TestAuditableEntity();
        var context = CreateMockContext(entity, EntityState.Unchanged);

        var eventData = new DbContextEventData(null!, null!, context: context.Object);
        var result = default(InterceptionResult<int>);

        await interceptor.SavingChangesAsync(eventData, result, CancellationToken.None);

        entity.Created.Should().Be(default);
        entity.CreatedBy.Should().BeNull();
        entity.LastModified.Should().Be(default);
        entity.LastModifiedBy.Should().BeNull();
    }

    private static Mock<DbContext> CreateMockContext(TestAuditableEntity entity, EntityState state)
    {
        var context = new Mock<DbContext>();
        var entry = new Mock<EntityEntry<BaseAuditableEntity>>();
        entry.Setup(e => e.Entity).Returns(entity);
        entry.Setup(e => e.State).Returns(state);
        entry.Setup(e => e.References).Returns(Enumerable.Empty<ReferenceEntry>());

        var entries = new List<EntityEntry<BaseAuditableEntity>> { entry.Object };
        context.Setup(c => c.ChangeTracker.Entries<BaseAuditableEntity>())
            .Returns(entries);

        return context;
    }

    private sealed class TestAuditableEntity : BaseAuditableEntity { }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
