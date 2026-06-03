using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class SessionTeamAssociationConfiguration : IEntityTypeConfiguration<SessionTeamAssociation>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SessionTeamAssociation> builder)
    {
        builder.ToTable("session_team_associations");

        builder.Ignore(association => association.Id);

        builder.HasKey(association => association.SessionTeamAssociationId);

        builder.Property(association => association.SessionTeamAssociationId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(association => association.LiveSessionId)
            .HasColumnName("live_session_id")
            .IsRequired();

        builder.Property(association => association.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.HasIndex(association => new { association.LiveSessionId, association.TeamId })
            .IsUnique();

        builder.HasOne<LiveSessionReference>()
            .WithMany(liveSessionReference => liveSessionReference.TeamAssociations)
            .HasForeignKey(association => association.LiveSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(association => association.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
