using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class RankingConfiguration : IEntityTypeConfiguration<Ranking>
{
    public void Configure(EntityTypeBuilder<Ranking> builder)
    {
        builder.ToTable("rankings");

        // Optimistic concurrency over Postgres's xmin system column — no stored column, no data
        // migration. A recalculation always rewrites GeneratedAt and CalculationVersion on the
        // principal row, so every save emits an UPDATE that the token guards: two recalcs loaded from
        // the same xmin cannot both commit, and the loser is retried against the winner's row.
        //
        // Mapped by hand because Npgsql dropped UseXminAsConcurrencyToken() in v9; this is the mapping
        // that helper used to generate. xmin already exists on every table as a system column, so the
        // migration must NOT emit an AddColumn for it.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

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

            rowBuilder.Property(row => row.TeamDisplayName)
                .HasColumnName("team_display_name")
                .HasMaxLength(200)
                .IsRequired();

            rowBuilder.HasIndex("ranking_id", nameof(Ranking.Row.Position));
        });

        builder.Navigation(ranking => ranking.Rows)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
