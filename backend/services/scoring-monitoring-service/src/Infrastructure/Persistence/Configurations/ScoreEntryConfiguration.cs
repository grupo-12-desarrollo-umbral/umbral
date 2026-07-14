using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class ScoreEntryConfiguration : IEntityTypeConfiguration<ScoreEntry>
{
    public void Configure(EntityTypeBuilder<ScoreEntry> builder)
    {
        builder.ToTable("score_entries");

        builder.Ignore(scoreEntry => scoreEntry.Id);

        builder.HasKey(scoreEntry => scoreEntry.ScoreEntryId);

        builder.Property(scoreEntry => scoreEntry.ScoreEntryId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(scoreEntry => scoreEntry.LiveSessionId)
            .HasColumnName("live_session_id")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.EntryType)
            .HasColumnName("entry_type")
            .HasConversion(
                entryType => entryType.ToString(),
                value => Enum.Parse<ScoreEntryType>(value))
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.ReasonCode)
            .HasColumnName("reason_code")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.ScoreValue)
            .HasColumnName("score_value")
            .HasConversion(
                scoreValue => scoreValue.Value,
                value => ScoreValue.Create(value))
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.RecordedAt)
            .HasColumnName("recorded_at")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.SourceEntityType)
            .HasColumnName("source_entity_type")
            .HasConversion(
                sourceType => sourceType.ToString(),
                value => Enum.Parse<ScoreSourceType>(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.SourceEntityId)
            .HasColumnName("source_entity_id")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.RecordedByUserId)
            .HasColumnName("recorded_by_user_id");

        builder.Property(scoreEntry => scoreEntry.Created)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(scoreEntry => scoreEntry.LastModified)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(scoreEntry => scoreEntry.LastModifiedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(scoreEntry => new { scoreEntry.LiveSessionId, scoreEntry.TeamId });

        builder.HasIndex(scoreEntry => new { scoreEntry.SourceEntityType, scoreEntry.SourceEntityId })
            .IsUnique();
    }
}
