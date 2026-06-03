using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.Ignore(team => team.Id);

        builder.HasKey(team => team.TeamId);

        builder.Property(team => team.TeamId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(team => team.DisplayName)
            .HasColumnName("display_name")
            .IsRequired();

        builder.Property(team => team.TeamCode)
            .HasColumnName("team_code")
            .IsRequired();

        builder.Property(team => team.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(team => team.Created)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(team => team.LastModified)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(team => team.CreatedBy)
            .HasColumnName("created_by");
        builder.Property(team => team.LastModifiedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(team => team.TeamCode)
            .IsUnique();

        builder.Navigation(team => team.Memberships)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
