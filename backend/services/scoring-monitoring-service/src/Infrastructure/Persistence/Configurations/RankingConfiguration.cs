using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class RankingConfiguration : IEntityTypeConfiguration<Ranking>
{
    public void Configure(EntityTypeBuilder<Ranking> builder)
    {
        builder.ToTable("rankings");

        builder.Ignore(ranking => ranking.Id);

        builder.HasKey(ranking => ranking.RankingId);

        builder.Property(ranking => ranking.RankingId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(ranking => ranking.LiveSessionId)
            .HasColumnName("live_session_id")
            .IsRequired();

        builder.Property(ranking => ranking.GeneratedAt)
            .HasColumnName("generated_at")
            .IsRequired();

        builder.Property(ranking => ranking.CalculationVersion)
            .HasColumnName("calculation_version")
            .IsRequired();

        builder.Property(ranking => ranking.Created)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(ranking => ranking.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(ranking => ranking.LastModified)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(ranking => ranking.LastModifiedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(ranking => ranking.LiveSessionId)
            .IsUnique();

        builder.OwnsMany(ranking => ranking.Rows, rowBuilder =>
        {
            rowBuilder.ToTable("ranking_rows");
            rowBuilder.WithOwner().HasForeignKey("ranking_id");

            rowBuilder.Ignore(row => row.Id);

            rowBuilder.Property<Guid>("ranking_id")
                .HasColumnName("ranking_id");

            rowBuilder.HasKey("ranking_id", nameof(Ranking.Row.TeamId));

            rowBuilder.Property(row => row.TeamId)
                .HasColumnName("team_id")
                .ValueGeneratedNever();

            rowBuilder.Property(row => row.Position)
                .HasColumnName("position")
                .IsRequired();

            rowBuilder.Property(row => row.TotalScore)
                .HasColumnName("total_score")
                .IsRequired();

            rowBuilder.Property(row => row.ResolutionTime)
                .HasColumnName("resolution_time")
                .IsRequired(false)
                .HasConversion(
                    resolutionTime => resolutionTime.Value,
                    value => value.HasValue
                        ? ResolutionTime.Comparable(value.Value)
                        : ResolutionTime.NonComparable());

            rowBuilder.HasIndex("ranking_id", nameof(Ranking.Row.Position));
        });

        builder.Navigation(ranking => ranking.Rows)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
