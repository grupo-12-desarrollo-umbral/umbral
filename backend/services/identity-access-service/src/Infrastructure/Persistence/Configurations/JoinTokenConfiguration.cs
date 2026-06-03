using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class JoinTokenConfiguration : IEntityTypeConfiguration<JoinToken>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<JoinToken> builder)
    {
        builder.ToTable("join_tokens");

        builder.Ignore(joinToken => joinToken.Id);
        builder.Ignore(joinToken => joinToken.Created);
        builder.Ignore(joinToken => joinToken.CreatedBy);
        builder.Ignore(joinToken => joinToken.LastModified);
        builder.Ignore(joinToken => joinToken.LastModifiedBy);

        builder.HasKey(joinToken => joinToken.JoinTokenId);

        builder.Property(joinToken => joinToken.JoinTokenId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(joinToken => joinToken.LiveSessionId)
            .HasColumnName("live_session_id")
            .IsRequired();

        builder.Property(joinToken => joinToken.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(joinToken => joinToken.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(joinToken => joinToken.IssuedAt)
            .HasColumnName("issued_at")
            .IsRequired();

        builder.Property(joinToken => joinToken.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(joinToken => joinToken.ConsumedAt)
            .HasColumnName("consumed_at");

        builder.Property(joinToken => joinToken.IssuedByUserId)
            .HasColumnName("issued_by_user_id")
            .IsRequired();

        builder.Property(joinToken => joinToken.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(joinToken => joinToken.TokenHash)
            .IsUnique();

        builder.HasIndex(joinToken => new { joinToken.LiveSessionId, joinToken.TeamId, joinToken.Status });
    }
}
