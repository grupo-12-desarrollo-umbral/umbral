using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

// Drives AuditableEntityInterceptor over a real EF change tracker (in-memory provider) so the
// interceptor sees genuine EntityEntry<BaseAuditableEntity> instances per EntityState branch —
// EntityEntry has no public ctor and cannot be mocked by Moq/Castle.
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
        await using var context = TrackedContext(entity, EntityState.Added);

        var eventData = new DbContextEventData(null!, null!, context: context);
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
        await using var context = TrackedContext(entity, EntityState.Modified);

        var eventData = new DbContextEventData(null!, null!, context: context);
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
        await using var context = TrackedContext(entity, EntityState.Unchanged);

        var eventData = new DbContextEventData(null!, null!, context: context);
        var result = default(InterceptionResult<int>);

        await interceptor.SavingChangesAsync(eventData, result, CancellationToken.None);

        entity.Created.Should().Be(default);
        entity.CreatedBy.Should().BeNull();
        entity.LastModified.Should().Be(default);
        entity.LastModifiedBy.Should().BeNull();
    }

    // Tracks a single auditable entity in the requested state on a fresh in-memory context, yielding a
    // real EntityEntry<BaseAuditableEntity> for the interceptor to walk. Non-Added states need a
    // non-default key so EF will track them as an existing row.
    private static TestDbContext TrackedContext(TestAuditableEntity entity, EntityState state)
    {
        var context = new TestDbContext(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        if (state != EntityState.Added)
        {
            entity.Id = 1;
        }

        context.Entry(entity).State = state;
        return context;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestAuditableEntity> Entities => Set<TestAuditableEntity>();
    }

    private sealed class TestAuditableEntity : BaseAuditableEntity { }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
