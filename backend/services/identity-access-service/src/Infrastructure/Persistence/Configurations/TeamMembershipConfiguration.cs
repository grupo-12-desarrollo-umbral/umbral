using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships");

        builder.Ignore(membership => membership.Id);

        builder.HasKey(membership => membership.TeamMembershipId);

        builder.Property(membership => membership.TeamMembershipId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(membership => membership.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(membership => membership.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.HasIndex(membership => new { membership.TeamId, membership.UserId })
            .IsUnique();

        builder.HasOne<Team>()
            .WithMany(team => team.Memberships)
            .HasForeignKey(membership => membership.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
