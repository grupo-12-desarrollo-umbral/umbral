using System.Reflection;
using umbral_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace umbral_backend.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Mission> Missions => Set<Mission>();

    public DbSet<TriviaQuiz> TriviaQuizzes => Set<TriviaQuiz>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
