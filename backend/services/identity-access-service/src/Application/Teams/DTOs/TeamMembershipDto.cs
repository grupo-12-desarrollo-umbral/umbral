namespace umbral_backend.Application.Teams.DTOs;

public sealed record TeamMembershipDto(
    Guid TeamMembershipId,
    Guid TeamId,
    int UserId,
    DateTimeOffset AssignedAt);
