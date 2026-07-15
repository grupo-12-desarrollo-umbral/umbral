using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class PenaltyConfiguration : IEntityTypeConfiguration<Penalty>
{
    public void Configure(EntityTypeBuilder<Penalty> builder)
    {
        builder.ToTable("penalties");

        builder.Ignore(penalty => penalty.Id);

        builder.HasKey(penalty => penalty.PenaltyId);

        builder.Property(penalty => penalty.PenaltyId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(penalty => penalty.ScoreEntryId)
            .HasColumnName("score_entry_id")
            .IsRequired();

        builder.Property(penalty => penalty.PenaltyReason)
            .HasColumnName("penalty_reason")
            .HasConversion(
                reason => reason.Value,
                value => PenaltyReason.Create(value))
            .IsRequired();

        builder.Property(penalty => penalty.AppliedAt)
            .HasColumnName("applied_at")
            .IsRequired();

        builder.Property(penalty => penalty.AppliedByUserId)
            .HasColumnName("applied_by_user_id")
            .IsRequired();

        builder.HasOne<ScoreEntry>()
            .WithOne()
            .HasForeignKey<Penalty>(p => p.ScoreEntryId)
            .IsRequired();
    }
}
