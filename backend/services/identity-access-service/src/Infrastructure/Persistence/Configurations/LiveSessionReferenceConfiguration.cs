using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class LiveSessionReferenceConfiguration : IEntityTypeConfiguration<LiveSessionReference>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<LiveSessionReference> builder)
    {
        builder.ToTable("live_sessions");

        builder.Ignore(liveSessionReference => liveSessionReference.Id);

        builder.HasKey(liveSessionReference => liveSessionReference.LiveSessionId);

        builder.Property(liveSessionReference => liveSessionReference.LiveSessionId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(liveSessionReference => liveSessionReference.SessionCode)
            .HasColumnName("session_code")
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(liveSessionReference => liveSessionReference.Created)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(liveSessionReference => liveSessionReference.LastModified)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(liveSessionReference => liveSessionReference.CreatedBy);
        builder.Ignore(liveSessionReference => liveSessionReference.LastModifiedBy);

        builder.HasIndex(liveSessionReference => liveSessionReference.SessionCode)
            .IsUnique();

        builder.Navigation(liveSessionReference => liveSessionReference.TeamAssociations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
