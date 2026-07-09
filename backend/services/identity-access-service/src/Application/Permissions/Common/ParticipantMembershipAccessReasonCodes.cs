namespace umbral_backend.Application.Permissions.Common;

public static class ParticipantMembershipAccessReasonCodes
{
    public const string Eligible = "eligible";
    public const string UserAccessDeactivated = "user-access-deactivated";
    public const string UserNotParticipant = "user-not-participant";
    public const string RegisteredTeamNotFound = "registered-team-not-found";
    public const string RegisteredTeamInactive = "registered-team-inactive";
    public const string ParticipantNotAuthorizedForRegisteredTeam = "participant-not-authorized-for-registered-team";
}
