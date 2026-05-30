using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.ExternalIdentityId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.Created)
            .IsRequired();

        builder.Property(user => user.LastModified)
            .IsRequired();

        builder.HasIndex(user => user.ExternalIdentityId)
            .IsUnique();

        builder.HasMany(user => user.IdentityProviderSessions)
            .WithOne()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(user => user.IdentityProviderSessions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
