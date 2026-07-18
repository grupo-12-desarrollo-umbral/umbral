using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class RegisteredTeamMembershipConfiguration : IEntityTypeConfiguration<RegisteredTeamMembership>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RegisteredTeamMembership> builder)
    {
        builder.ToTable("registered_team_memberships");

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

        builder.HasIndex(membership => new { membership.TeamId, membership.UserId })
            .IsUnique();

        builder.HasOne<RegisteredTeam>()
            .WithMany(team => team.Memberships)
            .HasForeignKey(membership => membership.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
