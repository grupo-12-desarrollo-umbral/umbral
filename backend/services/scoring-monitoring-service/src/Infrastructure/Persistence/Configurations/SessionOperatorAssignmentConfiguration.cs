using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class SessionOperatorAssignmentProjectionConfiguration : IEntityTypeConfiguration<SessionOperatorAssignmentProjection>
{
    public void Configure(EntityTypeBuilder<SessionOperatorAssignmentProjection> builder)
    {
        builder.ToTable("session_operator_assignments");

        builder.HasKey(assignment => assignment.LiveSessionId);

        builder.Property(assignment => assignment.LiveSessionId)
            .HasColumnName("live_session_id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.AssignedOperatorUserId)
            .HasColumnName("assigned_operator_user_id")
            .IsRequired();
    }
}
