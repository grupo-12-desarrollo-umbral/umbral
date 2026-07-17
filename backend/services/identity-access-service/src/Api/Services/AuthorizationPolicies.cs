namespace umbral_backend.Api.Services;

// Policy names for ASP.NET `[Authorize(Policy = ...)]` on controllers, mirroring each route's
// MediatR-level role requirement as a second layer. The MediatR AuthorizeAttribute stays the
// primary gate.
//
// There is deliberately no Participant policy. The participant-facing PermissionsController routes
// answer eligibility with a reason-coded 200 (`UserNotParticipant` — see
// ParticipantMembershipAccessAuthorizationProxy), not a 403, so gating them by role would break that
// contract. They take the plain authenticated-user check instead.
public static class AuthorizationPolicies
{
    public const string Administrator = nameof(Administrator);
    public const string AdminOrOperator = nameof(AdminOrOperator);
}
