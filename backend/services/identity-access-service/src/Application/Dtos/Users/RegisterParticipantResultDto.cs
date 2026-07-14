namespace umbral_backend.Application.Dtos.Users;

/// <summary>
/// Result of an anonymous participant self-registration (ADR-0016 §1): the Keycloak account has been
/// provisioned with the <c>Participant</c> role and a verification email dispatched. No local
/// <c>User</c> record exists yet — that is created on the first sign-in via
/// <c>POST /api/users/authenticated</c>, so only the email and server-fixed role are returned here.
/// </summary>
public sealed record RegisterParticipantResultDto(string Email, string Role);
