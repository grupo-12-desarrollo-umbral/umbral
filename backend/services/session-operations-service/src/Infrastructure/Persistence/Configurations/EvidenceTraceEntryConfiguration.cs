using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class EvidenceTraceEntryConfiguration : IEntityTypeConfiguration<EvidenceTraceEntry>
{
    public void Configure(EntityTypeBuilder<EvidenceTraceEntry> builder)
    {
        builder.ToTable("evidence_trace_entries");

        // EvidenceTraceEntry is a standalone projection (not an aggregate, not an owned child).
        // It inherits BaseEntity (not BaseAuditableEntity), so only BaseEntity.Id must be ignored.
        builder.Ignore(entry => entry.Id);

        builder.HasKey(entry => entry.EvidenceSubmissionId);

        builder.Property(entry => entry.EvidenceSubmissionId)
            .HasColumnName("evidence_submission_id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.LiveSessionId)
            .HasColumnName("live_session_id")
            .IsRequired();

        builder.Property(entry => entry.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(entry => entry.ActiveSubstageId)
            .HasColumnName("active_substage_id")
            .IsRequired();

        builder.Property(entry => entry.SubmissionType)
            .HasColumnName("submission_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entry => entry.SubmittedByParticipantId)
            .HasColumnName("submitted_by_participant_id");

        builder.Property(entry => entry.OriginReference)
            .HasColumnName("origin_reference")
            .HasMaxLength(256);

        builder.Property(entry => entry.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired();

        builder.Property(entry => entry.ValidationState)
            .HasColumnName("validation_state")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entry => entry.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(256);

        builder.Property(entry => entry.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.HasIndex(entry => new { entry.LiveSessionId, entry.TeamId });
    }
}
