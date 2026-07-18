using Microsoft.EntityFrameworkCore;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Application.UnitTests.Infrastructure.Persistence.Interceptors;

// Exercises every branch of EntityEntryExtensions.HasChangedOwnedEntities across a purpose-built
// model: the production identity model has no owned navigations, so the interceptor's owned-entity
// short-circuit (used to also audit a principal whose only change is in an owned member) is never
// reached through it. The helper is pure change-tracker metadata inspection, so an in-memory model
// is a faithful way to drive its three conditions (owned target present / owned / Added-or-Modified).
public sealed class EntityEntryExtensionsTests
{
    private sealed class Blog
    {
        public int Id { get; set; }
        public Metadata? Info { get; set; }        // owned reference
        public Author? Author { get; set; }        // non-owned reference
        public int? AuthorId { get; set; }
    }

    private sealed class Metadata
    {
        public string Slug { get; set; } = string.Empty;
    }

    private sealed class Author
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Author> Authors => Set<Author>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>().OwnsOne(blog => blog.Info);
            modelBuilder.Entity<Blog>().HasOne(blog => blog.Author).WithMany().HasForeignKey(blog => blog.AuthorId);
        }
    }

    private static TestDbContext NewContext() =>
        new(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void HasChangedOwnedEntities_AddedOwnedMember_ReturnsTrue()
    {
        using var context = NewContext();
        // Added owned Info (owned + Added) alongside a non-owned Author reference (owned == false):
        // both directions of the IsOwned() condition are exercised in one pass.
        var blog = new Blog { Info = new Metadata { Slug = "hello" }, Author = new Author { Name = "Ada" } };
        context.Add(blog);

        context.Entry(blog).HasChangedOwnedEntities().Should().BeTrue();
    }

    [Fact]
    public void HasChangedOwnedEntities_NoReferencesSet_ReturnsFalse()
    {
        using var context = NewContext();
        // No owned member and no author → every reference has a null TargetEntry.
        var blog = new Blog();
        context.Add(blog);

        context.Entry(blog).HasChangedOwnedEntities().Should().BeFalse();
    }

    [Fact]
    public void HasChangedOwnedEntities_UnchangedOwnedMember_ReturnsFalse()
    {
        using var context = NewContext();
        var blog = new Blog { Info = new Metadata { Slug = "hello" } };
        context.Add(blog);
        context.SaveChanges();
        // After save the owned target is Unchanged → present and owned, but not Added/Modified.
        context.Entry(blog).State.Should().Be(EntityState.Unchanged);

        context.Entry(blog).HasChangedOwnedEntities().Should().BeFalse();
    }
}
