namespace umbral_backend.Domain.Entities;

public sealed class SessionOperatorAssignmentProjection
{
    public Guid LiveSessionId { get; set; }

    public Guid AssignedOperatorUserId { get; set; }
}
