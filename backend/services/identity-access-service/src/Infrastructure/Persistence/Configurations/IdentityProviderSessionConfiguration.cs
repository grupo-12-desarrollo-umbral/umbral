using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class IdentityProviderSessionConfiguration : IEntityTypeConfiguration<IdentityProviderSession>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<IdentityProviderSession> builder)
    {
        builder.ToTable("identity_provider_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.ProviderName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(session => session.ProviderSessionKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(session => session.StartedAt)
            .IsRequired();

        builder.Property(session => session.ExpiresAt)
            .IsRequired();

        builder.Property(session => session.Created)
            .IsRequired();

        builder.Property(session => session.LastModified)
            .IsRequired();

        builder.HasIndex(session => new { session.UserId, session.ProviderName, session.ProviderSessionKey })
            .IsUnique();
    }
}
