namespace umbral_backend.Application.Teams.DTOs;

public sealed record TeamMembershipDto(
    Guid TeamMembershipId,
    Guid TeamId,
    int UserId,
    string Email,
    string DisplayName,
    DateTimeOffset AssignedAt);
