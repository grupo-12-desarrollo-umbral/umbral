using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public sealed class EntityEntryExtensionsTests
{
    private sealed class Blog
    {
        public int Id { get; set; }
        public Metadata? Info { get; set; }
        public Author? Author { get; set; }
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

    [Fact]
    public void HasChangedOwnedEntities_AddedOwnedMember_ReturnsTrue()
    {
        using var context = NewContext();
        var blog = new Blog { Info = new Metadata { Slug = "hello" }, Author = new Author { Name = "Ada" } };
        context.Add(blog);

        context.Entry(blog).HasChangedOwnedEntities().Should().BeTrue();
    }

    [Fact]
    public void HasChangedOwnedEntities_NoReferencesSet_ReturnsFalse()
    {
        using var context = NewContext();
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

        context.Entry(blog).HasChangedOwnedEntities().Should().BeFalse();
    }

    private static TestDbContext NewContext()
    {
        return new TestDbContext(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }
}
