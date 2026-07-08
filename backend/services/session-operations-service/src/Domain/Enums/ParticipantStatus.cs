namespace umbral_backend.Domain.Enums;

public enum ParticipantStatus
{
    Joined = 1,
    Active = 2,
    Disconnected = 3,
    Removed = 4,
    // Participation Block (#91): a deactivated/membership-revoked participant, recorded when a
    // runtime access re-check denies. Recovers to Active on reconnect if Users re-allows.
    Blocked = 5
}
