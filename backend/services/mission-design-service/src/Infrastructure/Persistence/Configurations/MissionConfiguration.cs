using umbral_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");

        builder.Property(mission => mission.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mission => mission.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(mission => mission.ActivationState)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.OwnsOne(mission => mission.Difficulty, difficultyBuilder =>
        {
            difficultyBuilder.Property(difficulty => difficulty.Value)
                .HasColumnName("Difficulty")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.OwnsOne(mission => mission.MaximumTime, maximumTimeBuilder =>
        {
            maximumTimeBuilder.Property(maximumTime => maximumTime.Minutes)
                .HasColumnName("MaximumTimeMinutes")
                .IsRequired();
        });
    }
}
