using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
{
    public const string SourceEventKeyIndexName = "ux_session_events_source_event_key";

    public void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        builder.ToTable("session_events");

        builder.HasKey(sessionEvent => sessionEvent.SessionEventId);

        builder.Property(sessionEvent => sessionEvent.SessionEventId)
            .HasColumnName("session_event_id")
            .ValueGeneratedNever();

        builder.Property(sessionEvent => sessionEvent.SourceEventKey)
            .HasColumnName("source_event_key")
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(sessionEvent => sessionEvent.SourceEventKey)
            .IsUnique()
            .HasDatabaseName(SourceEventKeyIndexName);

        builder.Property(sessionEvent => sessionEvent.LiveSessionId)
            .HasColumnName("live_session_id")
            .IsRequired();

        builder.HasIndex(sessionEvent => new { sessionEvent.LiveSessionId, sessionEvent.OccurredAt });

        builder.Property(sessionEvent => sessionEvent.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(sessionEvent => sessionEvent.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(sessionEvent => sessionEvent.PayloadSummary)
            .HasColumnName("payload_summary")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(sessionEvent => sessionEvent.ResponsibleUserId)
            .HasColumnName("responsible_user_id");
    }
}
