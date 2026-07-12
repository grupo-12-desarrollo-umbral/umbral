namespace umbral_backend.Application.Dtos.Users;

/// <summary>
/// Result of an admin-initiated invitation: the id of the local user record created in its pending
/// state, plus the invited email and role. The record is completed on the invitee's first sign-in.
/// </summary>
public sealed record InviteUserResultDto(int UserId, string Email, string Role);
