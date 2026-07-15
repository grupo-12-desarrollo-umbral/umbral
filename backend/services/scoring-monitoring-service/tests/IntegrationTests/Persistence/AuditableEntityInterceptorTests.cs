using Microsoft.EntityFrameworkCore.Diagnostics;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// Covers AuditableEntityInterceptor across its branches: Added (stamps Created + LastModified +
// CreatedBy + LastModifiedBy), Modified (advances LastModified + LastModifiedBy only), the
// unchanged-with-no-owned-changes skip, and the null-context guard on both sync and async entry
// points. An in-memory model keeps it broker/DB-free; a mutable FixedTimeProvider gives
// deterministic timestamps.
public sealed class AuditableEntityInterceptorTests
{
    private const string TestUserId = "test-user";

    private sealed class AuditableThing : BaseAuditableEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public string? Id => TestUserId;
        public string? Email => null;
        public string? Role => null;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AuditableThing> Things => Set<AuditableThing>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly StubCurrentUser DefaultUser = new();

    private static TestDbContext NewContext(AuditableEntityInterceptor interceptor) =>
        new(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options);

    [Fact]
    public async Task SavingChangesAsync_AddedEntity_StampsCreatedAndModified()
    {
        var created = DateTimeOffset.UnixEpoch;
        var interceptor = new AuditableEntityInterceptor(DefaultUser, new FixedTimeProvider(created));
        await using var context = NewContext(interceptor);
        var thing = new AuditableThing { Name = "one" };
        context.Add(thing);

        await context.SaveChangesAsync();

        thing.Created.Should().Be(created);
        thing.CreatedBy.Should().Be(TestUserId);
        thing.LastModified.Should().Be(created);
        thing.LastModifiedBy.Should().Be(TestUserId);
    }

    [Fact]
    public void SavingChanges_ModifiedEntity_AdvancesLastModifiedOnly()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var interceptor = new AuditableEntityInterceptor(DefaultUser, clock);
        using var context = NewContext(interceptor);
        var thing = new AuditableThing { Name = "one" };
        context.Add(thing);
        context.SaveChanges();
        var createdAt = thing.Created;

        clock.Now = createdAt.AddHours(1);
        thing.Name = "two";
        context.SaveChanges();

        thing.Created.Should().Be(createdAt);
        thing.CreatedBy.Should().Be(TestUserId);
        thing.LastModified.Should().Be(createdAt.AddHours(1));
        thing.LastModifiedBy.Should().Be(TestUserId);
    }

    [Fact]
    public void SavingChanges_UnchangedEntity_IsSkipped()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var interceptor = new AuditableEntityInterceptor(DefaultUser, clock);
        using var context = NewContext(interceptor);
        var thing = new AuditableThing { Name = "one" };
        context.Add(thing);
        context.SaveChanges();
        var stampedModified = thing.LastModified;

        // Second save has no pending change: the entity is Unchanged, so the interceptor skips it
        // and LastModified is not advanced despite the clock moving.
        clock.Now = stampedModified.AddHours(5);
        context.SaveChanges();

        thing.LastModified.Should().Be(stampedModified);
    }

    [Fact]
    public async Task NullContext_IsIgnoredOnBothEntryPoints()
    {
        var interceptor = new AuditableEntityInterceptor(DefaultUser, new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var eventData = new DbContextEventData(null!, null!, context: null);

        var sync = interceptor.SavingChanges(eventData, default);
        var async = await interceptor.SavingChangesAsync(eventData, default);

        sync.HasResult.Should().BeFalse();
        async.HasResult.Should().BeFalse();
    }
}
