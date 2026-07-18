namespace umbral_backend.Application.Dtos.Teams;

// A whitelist entry: this participant is authorized (may join) the registered team.
public sealed record RegisteredTeamMembershipDto(
    Guid TeamMembershipId,
    Guid TeamId,
    int UserId,
    string Email,
    string DisplayName);
