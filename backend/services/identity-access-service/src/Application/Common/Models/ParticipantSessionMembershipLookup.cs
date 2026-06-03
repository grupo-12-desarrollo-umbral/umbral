namespace umbral_backend.Application.Common.Models;

public sealed record ParticipantSessionMembershipLookup(
    Guid TeamId,
    Guid TeamMembershipId);
